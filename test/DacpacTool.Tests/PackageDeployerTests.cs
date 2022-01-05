using System;
using System.IO;
using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PackageDeployerTests
    {
        private readonly IConsole _console = Substitute.For<IConsole>();

        [TestMethod]
        public void UseTargetServer()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseTargetServer("localhost");

            // Assert
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldNotBeNull();
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldBe("localhost");
        }

        [TestMethod]
        public void UseTargetServerAndPort()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseTargetServerAndPort("localhost", 1432);

            // Assert
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldNotBeNull();
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldBe("localhost,1432");
        }

        [TestMethod]
        public void UseWindowsAuthentication()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseWindowsAuthentication();

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeTrue();
        }

        [TestMethod]
        public void UseSqlServerAuthenticationNoPasswordPrompts()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseSqlAuthentication("testuser", null);

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
            packageDeployer.ConnectionStringBuilder.UserID.ShouldBe("testuser");
            _console.Received().ReadLine();
        }

        [TestMethod]
        public void UseSqlServerAuthenticationNoPasswordSetsPassword()
        {
            // Arrange
            _console.ReadLine().Returns("testpassword");
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseSqlAuthentication("testuser", null);

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
            packageDeployer.ConnectionStringBuilder.UserID.ShouldBe("testuser");
            packageDeployer.ConnectionStringBuilder.Password.ShouldBe("testpassword");
        }

        [TestMethod]
        public void UseSqlAuthenticationWithPasswordDoesNotPrompt()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.UseSqlAuthentication("testuser", "testpassword");

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
            packageDeployer.ConnectionStringBuilder.UserID.ShouldBe("testuser");
            packageDeployer.ConnectionStringBuilder.Password.ShouldBe("testpassword");
            _console.DidNotReceive().ReadLine();
        }

        [TestMethod]
        public void SetSqlCmdVariable()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.SetSqlCmdVariable("MySqlCmdVariable", "SomeValue");

            // Assert
            packageDeployer.DeployOptions.SqlCommandVariableValues.ContainsKey("MySqlCmdVariable").ShouldBeTrue();
            packageDeployer.DeployOptions.SqlCommandVariableValues["MySqlCmdVariable"].ShouldBe("SomeValue");
        }

        [TestMethod]
        public void SetSqlCmdVariableNoValue()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.SetSqlCmdVariable("MySqlCmdVariable", string.Empty));

            // Assert
            packageDeployer.DeployOptions.SqlCommandVariableValues.ContainsKey("MySqlCmdVariable").ShouldBeFalse();
        }

        [TestMethod]
        public void DeployNoAuthentication()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            Should.Throw<InvalidOperationException>(() => packageDeployer.Deploy(packagePath, "TestDatabase"));
        }

        [TestMethod]
        public void DeployPackageDoesNotExist()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);
            packageDeployer.UseTargetServer("localhost");
            packageDeployer.UseWindowsAuthentication();

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.Deploy(new FileInfo("does-not-exist.dacpac"), "TestDatabase"));
        }

        [TestMethod]
        public void SetPropertySimpleValue()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.SetDeployProperties(new []{"AllowDropBlockingAssemblies=True"});

            // Assert
            packageDeployer.DeployOptions.AllowDropBlockingAssemblies.ShouldBeTrue();
        }

        [TestMethod]
        public void SetProperty_SqlCommandVariableValues_ShouldThrowException()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Assert
            Should.Throw<ArgumentException>(() => packageDeployer.SetDeployProperty("SqlCommandVariableValues=var1,var2"));
        }

        [TestMethod]
        public void SetPropertyInvalidFormat()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.SetDeployProperty("AllowDropBlockingAssemblies=ARandomString"));
        }

        [TestMethod]
        public void SetPropertyDoNotDropObjectTypes()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.SetDeployProperty("DoNotDropObjectTypes=Aggregates,Assemblies");

            // Assert
            packageDeployer.DeployOptions.DoNotDropObjectTypes.ShouldBe(new ObjectType[] { ObjectType.Aggregates, ObjectType.Assemblies });
        }

        [TestMethod]
        public void SetPropertyExcludeObjectTypes()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.SetDeployProperty("ExcludeObjectTypes=Contracts,Endpoints");

            // Assert
            packageDeployer.DeployOptions.ExcludeObjectTypes.ShouldBe(new ObjectType[] { ObjectType.Contracts, ObjectType.Endpoints });
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecification()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            packageDeployer.SetDeployProperty("DatabaseSpecification=Hyperscale,1024,P15");

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Hyperscale);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(1024);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBe("P15");
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationInvalidEdition()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.SetDeployProperty("DatabaseSpecification=MyFancyEdition;1024;P15"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationInvalidMaximumSize()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.SetDeployProperty("DatabaseSpecification=hyperscale;NotAnInteger;P15"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationTooFewParameters()
        {
            // Arrange
            var packageDeployer = new PackageDeployer(_console);

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.SetDeployProperty("DatabaseSpecification=hyperscale"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        private static FileInfo BuildSimpleModel()
        {
            var packagePath = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100)"))
                .AddStoredProcedure("csp_GetData", "SELECT * FROM dbo.TestTable")
                .SaveAsPackage();

            return new FileInfo(packagePath);
        }
    }
}
