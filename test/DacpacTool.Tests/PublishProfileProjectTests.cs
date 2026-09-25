using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PublishProfileProjectTests
    {
        private string directory;
        private string project;
        private string profilePath;

        [TestInitialize]
        public void Initialize()
        {
            directory = Directory.CreateTempSubdirectory("publish project ").FullName;
            project = Path.Combine(directory, "Consumer.csproj");
            profilePath = Path.Combine(directory, "Development profile.publish.xml");
            var sdk = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MSBuild.Sdk.SqlProj/Sdk"));
            new XDocument(new XElement("Project",
                new XElement("Import", new XAttribute("Project", Path.Combine(sdk, "Sdk.props"))),
                new XElement("PropertyGroup", new XElement("TargetFramework", "netstandard2.0")),
                new XElement("Import", new XAttribute("Project", Path.Combine(sdk, "Sdk.targets"))))).Save(project);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(directory, true);

        [TestMethod]
        public async Task CreateTarget_DoesNotBuildOrDeployAndRequiresExplicitOverwrite()
        {
            File.WriteAllText(Path.Combine(directory, "Invalid.sql"), "This SQL cannot compile");
            // These characters must remain data when invoking dotnet, not shell syntax.
            var server = "server & `echo injected`";
            var database = "Reporting & \"quoted\" 日本語";
            SetProperties(new XElement("TargetServerName", server), new XElement("TargetDatabaseName", database));
            var result = await Run("msbuild", project, "-t:CreatePublishProfile", "-p:PublishProfile=Development profile.publish.xml");
            result.ExitCode.ShouldBe(0, result.Output);
            var profile = DacProfile.Load(profilePath);
            profile.TargetDatabaseName.ShouldBe(database);
            new SqlConnectionStringBuilder(profile.TargetConnectionString).DataSource.ShouldBe(server);
            Directory.Exists(Path.Combine(directory, "bin")).ShouldBeFalse();
            var original = File.ReadAllText(profilePath);
            var duplicate = await Run("msbuild", project, "-t:CreatePublishProfile", "-p:PublishProfile=Development profile.publish.xml");
            duplicate.ExitCode.ShouldNotBe(0, duplicate.Output);
            File.ReadAllText(profilePath).ShouldBe(original);
            var replace = await Run("msbuild", project, "-t:CreatePublishProfile", $"-p:PublishProfile={profilePath}",
                "-p:OverwritePublishProfile=true", "-p:TargetDatabaseName=Replaced");
            replace.ExitCode.ShouldBe(0, replace.Output);
            DacProfile.Load(profilePath).TargetDatabaseName.ShouldBe("Replaced");
        }

        [TestMethod]
        public async Task CreateTarget_IncludesProjectPortAndAllowsCommandLineOverride()
        {
            SetProperties(new XElement("TargetServerName", "localhost,1433"), new XElement("TargetPort", "15433"));
            var result = await Run("msbuild", project, "-t:CreatePublishProfile", $"-p:PublishProfile={profilePath}");
            result.ExitCode.ShouldBe(0, result.Output);
            new SqlConnectionStringBuilder(DacProfile.Load(profilePath).TargetConnectionString)
                .DataSource.ShouldBe("localhost,15433");
            result = await Run("msbuild", project, "-t:CreatePublishProfile", $"-p:PublishProfile={profilePath}",
                "-p:TargetPort=16433", "-p:OverwritePublishProfile=true");
            result.ExitCode.ShouldBe(0, result.Output);
            new SqlConnectionStringBuilder(DacProfile.Load(profilePath).TargetConnectionString)
                .DataSource.ShouldBe("localhost,16433");
        }

        [TestMethod]
        public async Task PasswordProperty_OverridesPasswordWithUserFromProfile()
        {
            File.WriteAllText(profilePath, "<Project><PropertyGroup><TargetConnectionString>Data Source=unused;User ID=profile-user;Password=old-placeholder</TargetConnectionString><TargetDatabaseName>db</TargetDatabaseName></PropertyGroup></Project>");
            var options = await ReadPublishOptions($"-p:PublishProfile={profilePath}", "-p:TargetPassword=new-placeholder");
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, options).ShouldBe("db");
            deployer.ConnectionStringBuilder.UserID.ShouldBe("profile-user");
            deployer.ConnectionStringBuilder.Password.ShouldBe("new-placeholder");
        }

        [TestMethod]
        public async Task ProfileSettings_WinOverSdkAndSqlCmdDefaults()
        {
            WriteProfile("ProfileServer", "ProfileDatabase", false);
            SetVariables("project default", null);
            var options = await ReadPublishOptions("-p:PublishProfile=Development profile.publish.xml");
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, options).ShouldBe("ProfileDatabase");
            deployer.ConnectionStringBuilder.DataSource.ShouldBe("ProfileServer");
            deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Mandatory);
            deployer.DeployOptions.IncludeCompositeObjects.ShouldBeFalse();
            deployer.DeployOptions.SqlCommandVariableValues["Environment"].ShouldBe("profile value");
            deployer.DeployOptions.SqlCommandVariableValues["OnlyInProject"].ShouldBe("project fallback");
        }

        [TestMethod]
        public async Task ExplicitProperties_OverrideProfileEvenWhenEqualToSdkDefaults()
        {
            WriteProfile("ProfileServer", "ProfileDatabase", false);
            SetProperties(new XElement("TargetServerName", "(local)"), new XElement("TargetDatabaseName", "Consumer"),
                new XElement("IncludeCompositeObjects", "true"), new XElement("CommandTimeout", "42"));
            SetVariables("project default", "project explicit");
            var options = await ReadPublishOptions($"-p:PublishProfile={profilePath}", "-p:TargetEncrypt=false", "-p:CommandTimeout=77");
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, options).ShouldBe("Consumer");
            deployer.ConnectionStringBuilder.DataSource.ShouldBe("(local)");
            deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Optional);
            deployer.DeployOptions.IncludeCompositeObjects.ShouldBeTrue();
            deployer.DeployOptions.CommandTimeout.ShouldBe(77);
            deployer.DeployOptions.SqlCommandVariableValues["Environment"].ShouldBe("project explicit");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task MissingProfileSettings_FallBackToSdkDefaults(bool withProfile)
        {
            if (withProfile)
            {
                File.WriteAllText(profilePath, "<Project><PropertyGroup><TargetConnectionString>Integrated Security=True</TargetConnectionString></PropertyGroup></Project>");
            }
            SetVariables("project default", null);
            var options = await ReadPublishOptions(withProfile ? [$"-p:PublishProfile={profilePath}"] : []);
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, options).ShouldBe("Consumer");
            deployer.ConnectionStringBuilder.DataSource.ShouldBe("(local)");
            deployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeTrue();
            deployer.DeployOptions.IncludeCompositeObjects.ShouldBeTrue();
            deployer.DeployOptions.SqlCommandVariableValues["Environment"].ShouldBe("project default");
            if (!withProfile)
            {
                deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Optional);
            }
        }

        [TestMethod]
        public async Task PublishCommand_PassesProfileAndOverridesToToolWithoutShellExpansion()
        {
            WriteProfile("ProfileServer", "ProfileDatabase", false);
            SetVariables("default", "space & `echo injected` = value");
            // Build and publish normally, then remove the DACPAC immediately
            // before deployment so this test can never connect to a database.
            var document = XDocument.Load(project);
            document.Root.Add(new XElement("Target", new XAttribute("Name", "RemovePackageBeforeDeployment"),
                new XAttribute("BeforeTargets", "PrepareSqlPublish"),
                new XElement("Delete", new XAttribute("Files", "$(TargetPath)"))));
            document.Save(project);
            var result = await Run("publish", project, "/t:PublishDatabase", "-p:PublishProfile=Development profile.publish.xml",
                "-p:TargetServerName=OverrideServer", "-p:TargetDatabaseName=OverrideDatabase", "-p:TargetEncrypt=false");
            result.ExitCode.ShouldNotBe(0, result.Output);
            result.Output.ShouldContain("Using target server 'OverrideServer'");
            result.Output.ShouldContain("Using encrypt: False");
            result.Output.ShouldContain("space & `echo injected` = value");
            result.Output.ShouldContain("does not exist");
            result.Output.ShouldNotContain("NETSDK1198");
        }

        [TestMethod]
        public async Task MissingProfile_FailsBeforeDeployment()
        {
            var result = await Run("msbuild", project, "-t:PrepareSqlPublish", "-p:PublishProfile=missing.publish.xml");
            result.ExitCode.ShouldNotBe(0, result.Output);
            result.Output.ShouldContain("Publish profile does not exist:");
            result.Output.ShouldContain(Path.Combine(directory, "missing.publish.xml"));
        }

        private void WriteProfile(string server, string database, bool includeComposite)
        {
            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("TargetConnectionString", $"Data Source={server};Integrated Security=True;Encrypt=True"),
                new XElement("TargetDatabaseName", database), new XElement("IncludeCompositeObjects", includeComposite)),
                new XElement("ItemGroup", new XElement("SqlCmdVariable", new XAttribute("Include", "Environment"),
                    new XElement("Value", "profile value"))))).Save(profilePath);
        }

        private void SetProperties(params XElement[] properties)
        {
            var document = XDocument.Load(project);
            document.Root.Element("PropertyGroup").Add(properties);
            document.Save(project);
        }

        private void SetVariables(string defaultValue, string value)
        {
            var document = XDocument.Load(project);
            document.Root.Elements("Import").Last().AddBeforeSelf(new XElement("ItemGroup",
                new XElement("SqlCmdVariable", new XAttribute("Include", "Environment"),
                    new XElement("DefaultValue", defaultValue), value == null ? null : new XElement("Value", value)),
                new XElement("SqlCmdVariable", new XAttribute("Include", "OnlyInProject"), new XElement("DefaultValue", "project fallback"))));
            document.Save(project);
        }

        private async Task<DeployOptions> ReadPublishOptions(params string[] properties)
        {
            var result = await Run(["msbuild", project, "-t:PrepareSqlPublish", "-getItem:_SqlProjPublishArgument", .. properties]);
            result.ExitCode.ShouldBe(0, result.Output);
            using var snapshot = JsonDocument.Parse(result.Output[result.Output.IndexOf('{')..]);
            var options = new DeployOptions();
            var deployProperties = new List<string>();
            var defaultProperties = new List<string>();
            var variables = new List<string>();
            var defaultVariables = new List<string>();
            foreach (var item in snapshot.RootElement.GetProperty("Items").GetProperty("_SqlProjPublishArgument").EnumerateArray())
            {
                var value = item.GetProperty("Value").GetString();
                switch (item.GetProperty("Identity").GetString())
                {
                    case "--input": options.Input = new FileInfo(value); break;
                    case "--profile": options.Profile = new FileInfo(value); break;
                    case "--targetServerName": options.TargetServerName = value; break;
                    case "--defaultTargetServerName": options.DefaultTargetServerName = value; break;
                    case "--targetDatabaseName": options.TargetDatabaseName = value; break;
                    case "--defaultTargetDatabaseName": options.DefaultTargetDatabaseName = value; break;
                    case "--targetPassword": options.TargetPassword = value; break;
                    case "--encrypt": options.Encrypt = bool.Parse(value); break;
                    case "--property": deployProperties.Add(value); break;
                    case "--defaultProperty": defaultProperties.Add(value); break;
                    case "--sqlcmdvar": variables.Add(value); break;
                    case "--defaultSqlCmdVar": defaultVariables.Add(value); break;
                    default: Assert.Fail("Unexpected publish argument"); break;
                }
            }
            options.Property = deployProperties.ToArray();
            options.DefaultProperty = defaultProperties.ToArray();
            options.SqlCmdVar = variables.ToArray();
            options.DefaultSqlCmdVar = defaultVariables.ToArray();
            return options;
        }

        private static async Task<(int ExitCode, string Output)> Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetTempPath(), UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            start.ArgumentList.Add("-nologo");
            start.ArgumentList.Add($"-p:DacpacToolExe={typeof(Program).Assembly.Location}");
            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }
    }
}
