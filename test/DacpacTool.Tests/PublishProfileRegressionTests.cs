using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PublishProfileRegressionTests
    {
        [TestMethod]
        [DataRow("cli", "profile", "catalog", "cli")]
        [DataRow(null, "profile", "catalog", "profile")]
        [DataRow(null, null, "catalog", "catalog")]
        [DataRow(null, "", "catalog", "catalog")]
        [DataRow(null, "profile", "", "profile")]
        public void TargetPrecedence_PreservesPreDeploymentConnection(string cli, string profile, string catalog, string expected)
        {
            WithProfile($"Data Source=unused;Integrated Security=True;Initial Catalog={catalog}", profile, null, path =>
            {
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions { Profile = path, TargetDatabaseName = cli }).ShouldBe(expected);
                deployer.ConnectionStringBuilder.InitialCatalog.ShouldBe(catalog);
                deployer.GetScriptConnectionString(expected, true).InitialCatalog.ShouldBe(catalog);
                deployer.GetScriptConnectionString(expected, false).InitialCatalog.ShouldBe(expected);
                deployer.ConnectionStringBuilder.InitialCatalog.ShouldBe(catalog);
            });
        }

        [TestMethod]
        [DataRow("User ID=test-user")]
        [DataRow("User ID=test-user;Password=old-placeholder")]
        [DataRow("Authentication=Sql Password;User ID=test-user;Password=old-placeholder")]
        public void PasswordOverride_PreservesProfileUserAndAuthentication(string authentication)
        {
            WithProfile($"Data Source=unused;{authentication}", "db", null, path =>
            {
                var original = File.ReadAllText(path.FullName);
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions
                {
                    Profile = path, TargetPassword = "new-placeholder",
                }).ShouldBe("db");
                deployer.ConnectionStringBuilder.UserID.ShouldBe("test-user");
                deployer.ConnectionStringBuilder.Password.ShouldBe("new-placeholder");
                deployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
                var expected = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(authentication);
                deployer.ConnectionStringBuilder.Authentication.ShouldBe(expected.Authentication);
                File.ReadAllText(path.FullName).ShouldBe(original);
            });
        }

        [TestMethod]
        public void ReferenceScripts_PreserveDefaultDatabaseWithoutProfile()
        {
            var deployer = new PackageDeployer(new TestConsole());
            Program.ConfigureDeployer(deployer, new DeployOptions { TargetServerName = "unused", TargetDatabaseName = "new_database" });
            deployer.ConnectionStringBuilder.InitialCatalog.ShouldBeEmpty();
            deployer.GetScriptConnectionString("new_database", true).InitialCatalog.ShouldBeEmpty();
            deployer.GetScriptConnectionString("new_database", false).InitialCatalog.ShouldBe("new_database");
            deployer.ConnectionStringBuilder.InitialCatalog.ShouldBeEmpty();
        }

        [TestMethod]
        [DataRow("CommandTimeout", "77")]
        [DataRow("LongRunningCommandTimeout", "88")]
        [DataRow("DatabaseLockTimeout", "99")]
        [DataRow("AdditionalDeploymentContributorPaths", "/unused/a;/unused/b")]
        [DataRow("RebuildIndexesOfflineForDataPhase", "True")]
        [DataRow("DisableParallelismForEnablingIndexes", "True")]
        public void Profile_PreservesDeployOptions(string name, string value)
        {
            WithProfile("Data Source=unused;Integrated Security=True", "db", new XElement(name, value), path =>
            {
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions { Profile = path });
                var expected = new Microsoft.SqlServer.Dac.DacDeployOptions();
                expected.SetDeployProperty(name, value);
                var property = expected.GetType().GetProperty(name);
                property.GetValue(deployer.DeployOptions).ShouldBe(property.GetValue(expected));
            });
        }

        [TestMethod]
        [DataRow("UnknownSafetyOption", "True")]
        [DataRow("DeployScriptFileName", "ignored.sql")]
        [DataRow("CommandTimeout", "not-an-integer")]
        public void Profile_RejectsUnconsumedOrInvalidOptions(string name, string value)
        {
            WithProfile("Data Source=unused;Integrated Security=True", "db", new XElement(name, value), path =>
            {
                Should.Throw<ArgumentException>(() => Program.ConfigureDeployer(new PackageDeployer(new TestConsole()),
                    new DeployOptions { Profile = path })).Message.ShouldContain(name);
            });
        }

        [TestMethod]
        [DataRow("Active Directory Default")]
        [DataRow("Active Directory Managed Identity")]
        [DataRow("Active Directory Integrated")]
        [DataRow("Active Directory Interactive")]
        public void UnsupportedAuthentication_RejectsDuringConfiguration(string authentication)
        {
            WithProfile($"Data Source=unused;Authentication={authentication}", "db", null, path =>
            {
                Should.Throw<ArgumentException>(() => Program.ConfigureDeployer(new PackageDeployer(new TestConsole()),
                    new DeployOptions { Profile = path })).Message.ShouldContain("authentication");
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions
                {
                    Profile = path, TargetUser = "test-user", TargetPassword = "test-only-password",
                }).ShouldBe("db");
                deployer.ConnectionStringBuilder.Authentication.ShouldBe(Microsoft.Data.SqlClient.SqlAuthenticationMethod.NotSpecified);
            });
        }

        [TestMethod]
        [DataRow("<PropertyGroup Condition=\"false\"><BlockOnPossibleDataLoss>False</BlockOnPossibleDataLoss></PropertyGroup>", "Condition")]
        [DataRow("<Import Project=\"unused.props\" />", "Project")]
        [DataRow("<PropertyGroup><CommandTimeout>77</CommandTimeout><commandtimeout>88</commandtimeout></PropertyGroup>", "commandtimeout")]
        [DataRow("<PropertyGroup><CommandTimeout><Value>77</Value></CommandTimeout></PropertyGroup>", "CommandTimeout")]
        [DataRow("<PropertyGroup><ProfileVersionNumber>2</ProfileVersionNumber></PropertyGroup>", "ProfileVersionNumber")]
        [DataRow("<ItemGroup><SqlCmdVariable Include=\"x\"><Value>a</Value><Value>b</Value></SqlCmdVariable></ItemGroup>", "SqlCmdVariable")]
        [DataRow("<ItemGroup><SqlCmdVariable Include=\"x\"><Value>a</Value></SqlCmdVariable><SqlCmdVariable Include=\"x\"><Value>b</Value></SqlCmdVariable></ItemGroup>", "x")]
        public void Profile_RejectsAmbiguousOrUnsupportedStructure(string content, string diagnostic)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, $"<Project>{content}</Project>");
                Should.Throw<ArgumentException>(() => PublishProfileReader.Load(path)).Message.ShouldContain(diagnostic);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Profile_OverridesRetainedOptionsAndDoesNotEchoInvalidValues()
        {
            WithProfile("Data Source=unused;Integrated Security=True", "db", new XElement("CommandTimeout", "77"), path =>
            {
                var original = File.ReadAllText(path.FullName);
                var deployer = new PackageDeployer(new TestConsole());
                Program.ConfigureDeployer(deployer, new DeployOptions { Profile = path, Property = ["CommandTimeout=88"] });
                deployer.DeployOptions.CommandTimeout.ShouldBe(88);
                File.ReadAllText(path.FullName).ShouldBe(original);
            });
            WithProfile("Data Source=unused;Integrated Security=True", "db", new XElement("CommandTimeout", "sensitive-invalid-value"), path =>
            {
                Should.Throw<ArgumentException>(() => PublishProfileReader.Load(path.FullName)).ToString().ShouldNotContain("sensitive-invalid-value");
            });
        }

        [TestMethod]
        [DataRow("Integrated Security=True")]
        [DataRow("User ID=test;Password=test-only-password")]
        [DataRow("Authentication=Sql Password;User ID=test;Password=test-only-password")]
        public void SupportedAuthentication_ReachesMissingPackageValidationWithoutConnecting(string authentication)
        {
            WithProfile($"Data Source=unused;{authentication}", "db", null, path =>
            {
                var deployer = new PackageDeployer(new TestConsole());
                var target = Program.ConfigureDeployer(deployer, new DeployOptions { Profile = path });
                Should.Throw<ArgumentException>(() => deployer.Deploy(new FileInfo(path.FullName + ".missing.dacpac"), target))
                    .Message.ShouldContain("does not exist");
            });
        }

        [TestMethod]
        [DataRow(null, "", null)]
        [DataRow("", "profile", "catalog")]
        public void MissingOrExplicitlyEmptyTarget_IsRejected(string cli, string profile, string catalog)
        {
            WithProfile($"Data Source=unused;Integrated Security=True;Initial Catalog={catalog}", profile, null, path =>
            {
                Should.Throw<ArgumentException>(() => Program.ConfigureDeployer(new PackageDeployer(new TestConsole()),
                    new DeployOptions { Profile = path, TargetDatabaseName = cli }));
            });
        }

        private static void WithProfile(string connection, string database, XElement property, Action<FileInfo> test)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.publish.xml");
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
            if (property != null)
            {
                property.Name = ns + property.Name.LocalName;
            }
            try
            {
                new XDocument(new XElement(ns + "Project", new XAttribute("ToolsVersion", "4.0"),
                    new XElement(ns + "PropertyGroup",
                        new XElement(ns + "TargetConnectionString", connection),
                        database == null ? null : new XElement(ns + "TargetDatabaseName", database),
                        new XElement(ns + "ProfileVersionNumber", "1"), property))).Save(path);
                test(new FileInfo(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
