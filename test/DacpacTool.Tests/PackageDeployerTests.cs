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
        private IConsole _console = Substitute.For<IConsole>();

        [TestMethod]
        public void LoadPackage()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);

            // Assert
            packageDeployer.Package.ShouldNotBeNull();
        }

        [TestMethod]
        public void LoadPackageFileDoesNotExist()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = "SomeDummyFile.dacpac";

            // Act
            Should.Throw<ArgumentException>(() => packageDeployer.LoadPackage(new FileInfo(packagePath)));

            // Assert
            packageDeployer.Package.ShouldBeNull();
        }

        [TestMethod]
        public void UseTargetServer()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.UseTargetServer("localhost");

            // Assert
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldNotBeNull();
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldBe("localhost");
        }

        [TestMethod]
        public void UseTargetServerWithoutLoadPackage()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            Should.Throw<InvalidOperationException>(() => packageDeployer.UseTargetServer("localhost"));

            // Assert
            packageDeployer.ConnectionStringBuilder.DataSource.ShouldBeEmpty();
        }

        [TestMethod]
        public void UseWindowsAuthentication()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.UseWindowsAuthentication();

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeTrue();
        }

        [TestMethod]
        public void UseWindowsAuthenticationWithoutLoadPackage()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            Should.Throw<InvalidOperationException>(() => packageDeployer.UseWindowsAuthentication());

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
        }

        [TestMethod]
        public void UseSqlServerAuthenticationNoPasswordPrompts()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
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
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
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
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.UseSqlAuthentication("testuser", "testpassword");

            // Assert
            packageDeployer.ConnectionStringBuilder.IntegratedSecurity.ShouldBeFalse();
            packageDeployer.ConnectionStringBuilder.UserID.ShouldBe("testuser");
            packageDeployer.ConnectionStringBuilder.Password.ShouldBe("testpassword");
            _console.DidNotReceive().ReadLine();
        }

        [TestMethod]
        public void DeployNoPackageLoaded()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            Should.Throw<InvalidOperationException>(() => packageDeployer.Deploy("TestDatabase"));

            // Assert
            // Should throw
        }

        [TestMethod]
        public void DeployNoAuthentication()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            Should.Throw<InvalidOperationException>(() => packageDeployer.Deploy("TestDatabase"));
        }

        [TestMethod]
        public void SetPropertySimpleValue()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.SetProperty("AllowDropBlockingAssemblies", "true");

            // Assert
            packageDeployer.DeployOptions.AllowDropBlockingAssemblies.ShouldBeTrue();
        }

        [TestMethod]
        public void SetPropertyInvalidFormat()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            Should.Throw<ArgumentException>(() => packageDeployer.SetProperty("AllowDropBlockingAssemblies", "ARandomString"));
        }

        [TestMethod]
        public void SetPropertyDoNotDropObjectTypes()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.SetProperty("DoNotDropObjectTypes", "Aggregates;Assemblies");

            // Assert
            packageDeployer.DeployOptions.DoNotDropObjectTypes.ShouldBe(new ObjectType[] { ObjectType.Aggregates, ObjectType.Assemblies });
        }

        [TestMethod]
        public void SetPropertyExcludeObjectTypes()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.SetProperty("ExcludeObjectTypes", "Contracts;Endpoints");

            // Assert
            packageDeployer.DeployOptions.ExcludeObjectTypes.ShouldBe(new ObjectType[] { ObjectType.Contracts, ObjectType.Endpoints });
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecification()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            packageDeployer.SetProperty("DatabaseSpecification", "Hyperscale;1024;P15");

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Hyperscale);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(1024);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBe("P15");
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationInvalidEdition()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            Should.Throw<ArgumentException>(() => packageDeployer.SetProperty("DatabaseSpecification", "MyFancyEdition;1024;P15"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationInvalidMaximumSize()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            Should.Throw<ArgumentException>(() => packageDeployer.SetProperty("DatabaseSpecification", "hyperscale;NotAnInteger;P15"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        [TestMethod]
        public void SetPropertyDatabaseSpecificationTooFewParameters()
        {
            // Arrange
            using var packageDeployer = new PackageDeployer(_console);
            var packagePath = BuildSimpleModel();

            // Act
            packageDeployer.LoadPackage(packagePath);
            Should.Throw<ArgumentException>(() => packageDeployer.SetProperty("DatabaseSpecification", "hyperscale"));

            // Assert
            packageDeployer.DeployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.Default);
            packageDeployer.DeployOptions.DatabaseSpecification.MaximumSize.ShouldBe(default);
            packageDeployer.DeployOptions.DatabaseSpecification.ServiceObjective.ShouldBeNull();
        }

        private FileInfo BuildSimpleModel()
        {
            var packagePath = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100)"))
                .AddStoredProcedure("csp_GetData", "SELECT * FROM dbo.TestTable")
                .SaveAsPackage();

            return new FileInfo(packagePath);
        }
    }
}
