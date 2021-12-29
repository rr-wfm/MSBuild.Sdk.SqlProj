using System;
using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PropertyParserTest
    {
        [TestMethod]
        public void ToDacDeployOptions_WithBooleanProperties_ShouldParse()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[]
            {
                "CreateNewDatabase=True",
                "IgnoreAuthorizer=True"
            } };

            // Act
            var deployOptions = buildOptions.ToDacDeployOptions();

            // Assert
            deployOptions.CreateNewDatabase.ShouldBeTrue();
            deployOptions.IgnoreAuthorizer.ShouldBeTrue();
        }

        [TestMethod]
        public void ToDacDeployOptions_WithIntegerProperty_ShouldParse()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { "CommandTimeout=200" } };

            // Act
            var deployOptions = buildOptions.ToDacDeployOptions();

            // Assert
            deployOptions.CommandTimeout.ShouldBe(200);
        }

        [TestMethod]
        public void ToDacDeployOptions_WithObjectTypesProperties_ShouldParse()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[]
            {
                $"ExcludeObjectTypes={ObjectType.Audits},{ObjectType.Endpoints},{ObjectType.Queues}",
                $"DoNotDropObjectTypes={ObjectType.Certificates},{ObjectType.Contracts}"
            } };

            // Act
            var deployOptions = buildOptions.ToDacDeployOptions();

            // Assert
            deployOptions.ExcludeObjectTypes.Length.ShouldBe(3);
            deployOptions.ExcludeObjectTypes.ShouldContain(ObjectType.Audits);
            deployOptions.ExcludeObjectTypes.ShouldContain(ObjectType.Endpoints);
            deployOptions.ExcludeObjectTypes.ShouldContain(ObjectType.Queues);

            deployOptions.DoNotDropObjectTypes.Length.ShouldBe(2);
            deployOptions.DoNotDropObjectTypes.ShouldContain(ObjectType.Certificates);
            deployOptions.DoNotDropObjectTypes.ShouldContain(ObjectType.Contracts);
        }

        [TestMethod]
        [DataRow("ExcludeObjectTypes=Audits;Endpoints;Queues", DisplayName = "Incorrect separator")]
        [DataRow("ExcludeObjectTypes=P1,P2,P3", DisplayName = "Unknown object types")]
        public void ToDacDeployOptions_WithIncorrectObjectTypesProperty_ShouldThrowException(string property)
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { property } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void ToDacDeployOptions_WithDacAzureDatabaseSpecificationProperty_ShouldParse()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { $"DatabaseSpecification={DacAzureEdition.DataWarehouse},20,S3" } };

            // Act
            var deployOptions = buildOptions.ToDacDeployOptions();

            // Assert
            deployOptions.DatabaseSpecification.ShouldNotBeNull();
            deployOptions.DatabaseSpecification.Edition.ShouldBe(DacAzureEdition.DataWarehouse);
            deployOptions.DatabaseSpecification.MaximumSize.ShouldBe(20);
            deployOptions.DatabaseSpecification.ServiceObjective.ShouldBe("S3");
        }

        [TestMethod]
        [DataRow("DataWarehouse;20;S3", "Expected database specification to be comma-seperated instead of semi-colon separated", DisplayName = "Incorrect separator")]
        [DataRow("DataWarehouse,20", "Expected at least 3 parameters for", DisplayName = "Incorrect number of parameter")]
        [DataRow("AwesomeEdition,20,S2", "Unknown edition", DisplayName = "Unknown database edition")]
        [DataRow("DataWarehouse,X20,S3", "Unable to parse maximum size", DisplayName = "Incorrect database max size")]
        public void ToDacDeployOptions_WithIncorrectDacAzureDatabaseSpecificationProperty_ShouldThrowException(string propertyValue, string exceptionMessage)
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { $"DatabaseSpecification={propertyValue}" } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldThrow<ArgumentException>().Message.ShouldStartWith(exceptionMessage);
        }

        [TestMethod]
        public void ToDacDeployOptions_WithIncorrectIntegerProperty_ShouldThrowException()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { "CommandTimeout=WrongValue" } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void ToDacDeployOptions_WithIncorrectBooleanProperty_ShouldThrowException()
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { "CreateNewDatabase=WrongValue" } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldThrow<FormatException>();
        }

        [TestMethod]
        [DataRow("Unknown -> WrongValue", typeof(ArgumentException))]
        [DataRow("=", typeof(ArgumentException))]
        [DataRow("UnknownProperty=212", typeof(ArgumentException))]
        public void ToDacDeployOptions_WithWronglyFormattedOrhUnknownProperty_ShouldThrowException(string property, Type exceptionType)
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { property } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldThrow(exceptionType);
        }

        [TestMethod]
        [DataRow(" ")]
        [DataRow(null)]
        [DataRow("")]
        public void ToDacDeployOptions_WithNullOrEmptyProperty_ShouldNotThrowException(string property)
        {
            // Arrange
            var buildOptions = new BuildOptions { DeployProperty = new[] { property } };

            // Act
            Action action = () => buildOptions.ToDacDeployOptions();

            // Assert
            action.ShouldNotThrow();
        }
    }
}
