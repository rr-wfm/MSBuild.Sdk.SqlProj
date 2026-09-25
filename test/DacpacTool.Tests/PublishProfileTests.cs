using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PublishProfileTests
    {
        [TestMethod]
        public void Create_RoundTripsThroughDacFxAndDeploymentConfiguration()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.publish.xml");
            try
            {
                var options = new CreatePublishProfileOptions
                {
                    Output = new FileInfo(path),
                    TargetServerName = "server;&<\"",
                    TargetDatabaseName = "database & <test> \"日本語\"",
                };
                PublishProfileWriter.Create(options);
                var profile = DacProfile.Load(path);
                profile.TargetDatabaseName.ShouldBe(options.TargetDatabaseName);
                profile.DeployOptions.BlockOnPossibleDataLoss.ShouldBeTrue();
                profile.DeployOptions.DropObjectsNotInSource.ShouldBeFalse();
                var connection = new SqlConnectionStringBuilder(profile.TargetConnectionString);
                connection.DataSource.ShouldBe(options.TargetServerName);
                connection.Encrypt.ShouldBe(SqlConnectionEncryptOption.Mandatory);
                connection.TrustServerCertificate.ShouldBeFalse();
                connection.IntegratedSecurity.ShouldBeTrue();
                connection.UserID.ShouldBeEmpty();
                connection.Password.ShouldBeEmpty();
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions { Profile = new FileInfo(path) })
                    .ShouldBe(options.TargetDatabaseName);
                deployer.ConnectionStringBuilder.DataSource.ShouldBe(options.TargetServerName);
                deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Mandatory);
                deployer.DeployOptions.BlockOnPossibleDataLoss.ShouldBeTrue();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Configure_ProfileAllowsExplicitOverridesWithoutRewritingFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.publish.xml");
            try
            {
                PublishProfileWriter.Create(new CreatePublishProfileOptions
                {
                    Output = new FileInfo(path), TargetServerName = "original,1433", TargetDatabaseName = "original",
                });
                var document = XDocument.Load(path);
                XNamespace ns = document.Root.Name.Namespace;
                document.Root.Add(new XElement(ns + "ItemGroup",
                    new XElement(ns + "SqlCmdVariable", new XAttribute("Include", "Environment"), new XElement(ns + "Value", "original")),
                    new XElement(ns + "SqlCmdVariable", new XAttribute("Include", "Retained"), new XElement(ns + "Value", "retained"))));
                document.Save(path);
                var original = File.ReadAllText(path);
                var portOverride = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(portOverride, new DeployOptions { Profile = new FileInfo(path), TargetPort = 1444 });
                portOverride.ConnectionStringBuilder.DataSource.ShouldBe("original,1444");
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions
                {
                    Profile = new FileInfo(path), TargetServerName = "override", TargetDatabaseName = "override",
                    TargetPort = 1434, TargetUser = "test-user", TargetPassword = "test-only-password",
                    Encrypt = false, Property = ["BlockOnPossibleDataLoss=False"], SqlCmdVar = ["Environment=test=value"],
                }).ShouldBe("override");
                deployer.ConnectionStringBuilder.DataSource.ShouldBe("override,1434");
                deployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
                deployer.ConnectionStringBuilder.UserID.ShouldBe("test-user");
                deployer.ConnectionStringBuilder.Password.ShouldBe("test-only-password");
                deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Optional);
                deployer.DeployOptions.BlockOnPossibleDataLoss.ShouldBeFalse();
                deployer.DeployOptions.SqlCommandVariableValues["Environment"].ShouldBe("test=value");
                deployer.DeployOptions.SqlCommandVariableValues["Retained"].ShouldBe("retained");
                File.ReadAllText(path).ShouldBe(original);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Configure_WithoutProfileRetainsLegacyDefaultsAndValidatesTargets()
        {
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, new DeployOptions
            {
                TargetServerName = "localhost", TargetDatabaseName = "test",
            }).ShouldBe("test");
            deployer.ConnectionStringBuilder.Encrypt.ShouldBe(SqlConnectionEncryptOption.Optional);
            deployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeTrue();
            Should.Throw<ArgumentException>(() => Program.ConfigureDeployer(deployer, new DeployOptions()));
            Should.Throw<ArgumentException>(() => Program.ConfigureDeployer(deployer, new DeployOptions
            {
                TargetServerName = "localhost", TargetDatabaseName = "test", TargetPort = 65536,
            }));
        }

        [TestMethod]
        public async Task Cli_CreatesAndProtectsProfileAndRejectsCredentials()
        {
            var directory = Directory.CreateTempSubdirectory("publish profile ");
            var path = Path.Combine(directory.FullName, "test profile.publish.xml");
            try
            {
                string[] arguments = ["create-publish-profile", "--output", path,
                    "--targetServerName", "localhost", "--targetDatabaseName", "test & <database>"];
                (await RunCli(arguments)).ExitCode.ShouldBe(0);
                DacProfile.Load(path).TargetDatabaseName.ShouldBe("test & <database>");
                var original = File.ReadAllText(path);
                (await RunCli(arguments)).ExitCode.ShouldBe(1);
                File.ReadAllText(path).ShouldBe(original);
                (await RunCli([.. arguments, "--force"])).ExitCode.ShouldBe(0);
                (await RunCli([.. arguments, "--targetPassword", "must-not-persist"])).ExitCode.ShouldNotBe(0);
                File.ReadAllText(path).ShouldNotContain("must-not-persist");
                (await RunCli(["create-publish-profile", "--output", path])).ExitCode.ShouldNotBe(0);

                // Missing input stops before network access, but exercises real deploy command parsing/configuration.
                var result = await RunCli(["deploy", "--profile", path, "--input", Path.Combine(directory.FullName, "missing.dacpac")]);
                result.ExitCode.ShouldBe(1);
                result.Output.ShouldContain("does not exist");
                result.Output.ShouldContain("Using target server 'localhost'");
                var overrideResult = await RunCli(["deploy", "--profile", path, "--input", Path.Combine(directory.FullName, "missing.dacpac"), "--encrypt", "false"]);
                overrideResult.Output.ShouldContain("Using encrypt: False");
                overrideResult.Output.ShouldContain("does not exist");
                var legacyFlag = await RunCli(["deploy", "--input", Path.Combine(directory.FullName, "missing.dacpac"), "--targetServerName", "localhost", "--targetDatabaseName", "test", "--encrypt"]);
                legacyFlag.Output.ShouldContain("Using encrypt: True");
                legacyFlag.Output.ShouldContain("does not exist");
                Directory.GetFiles(directory.FullName, "*.tmp").ShouldBeEmpty();
            }
            finally
            {
                directory.Delete(true);
            }
        }

        private static async Task<(int ExitCode, string Output)> RunCli(string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add(typeof(PackageDeployer).Assembly.Location);
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start);
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }

        [TestMethod]
        public void Create_MissingParentAndInvalidServerDoNotCreateArtifacts()
        {
            var directory = Directory.CreateTempSubdirectory("publish-profile-");
            try
            {
                var options = new CreatePublishProfileOptions
                {
                    Output = new FileInfo(Path.Combine(directory.FullName, "missing", "test.publish.xml")),
                    TargetServerName = "localhost", TargetDatabaseName = "test",
                };
                Should.Throw<DirectoryNotFoundException>(() => PublishProfileWriter.Create(options));
                options.Output = new FileInfo(Path.Combine(directory.FullName, "test.publish.xml"));
                options.TargetServerName = " ";
                Should.Throw<ArgumentException>(() => PublishProfileWriter.Create(options));
                Directory.GetFileSystemEntries(directory.FullName).ShouldBeEmpty();
            }
            finally
            {
                directory.Delete(true);
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(65536)]
        public void Create_InvalidPortDoesNotDamageExistingFile(int port)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "keep me");
                Should.Throw<ArgumentException>(() => PublishProfileWriter.Create(new CreatePublishProfileOptions
                {
                    Output = new FileInfo(path), TargetServerName = "localhost", TargetDatabaseName = "test",
                    TargetPort = port, Force = true,
                }));
                File.ReadAllText(path).ShouldBe("keep me");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Create_RequiresForceToReplaceExistingFile()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "keep me");
                var options = new CreatePublishProfileOptions
                {
                    Output = new FileInfo(path), TargetServerName = "localhost", TargetDatabaseName = "test",
                };
                Should.Throw<IOException>(() => PublishProfileWriter.Create(options));
                File.ReadAllText(path).ShouldBe("keep me");
                options.Force = true;
                PublishProfileWriter.Create(options);
                DacProfile.Load(path).TargetDatabaseName.ShouldBe("test");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("invalid\u0001")]
        public void Create_InvalidDatabaseDoesNotDamageExistingFile(string database)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "keep me");
                var options = new CreatePublishProfileOptions
                {
                    Output = new FileInfo(path), TargetServerName = "localhost", TargetDatabaseName = database, Force = true,
                };
                Should.Throw<ArgumentException>(() => PublishProfileWriter.Create(options));
                File.ReadAllText(path).ShouldBe("keep me");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
