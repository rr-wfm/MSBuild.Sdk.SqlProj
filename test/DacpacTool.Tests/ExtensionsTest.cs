using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    /// <summary>
    /// Contains tests for the <see cref="Extensions" /> class.
    /// </summary>
    [TestClass]
    public class ExtensionsTest
    {
        /// <summary>
        /// Tests that we can add a reference to a model.
        /// </summary>
        [TestMethod]
        public void CanAddReference()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage)
                .AddStoredProcedure("MyProc", "SELECT * FROM MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests that we can get references from a model.
        /// </summary>
        [TestMethod]
        public void CanGetReferences()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();
            var model = new TestModelBuilder()
                .AddReference(referencePackage)
                .Build();
            
            // Act
            var references = model.GetReferencedDacPackages();

            // Assert
            references.Any().ShouldBeTrue();
            references.First().ShouldBe(referencePackage.Replace('\\', Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Tests that we can get model validation errors from a model.
        /// </summary>
        [TestMethod]
        public void CanGetModelValidationErrors()
        {
            // Arrange
            var model = new TestModelBuilder()
                .AddStoredProcedureFromFile("../../../../TestProjectWithErrors/Procedures/csp_Test.sql")
                .Build();

            // Act
            var modelValidationErrors = model.GetModelValidationErrors(Enumerable.Empty<string>());

            // Assert
            modelValidationErrors.ShouldNotBeEmpty();

            var error = modelValidationErrors.First();
            error.Severity.ShouldBe(Microsoft.SqlServer.Dac.Model.ModelErrorSeverity.Error);
            error.ErrorCode.ShouldBe(71501);
            error.SourceName.ShouldBe("../../../../TestProjectWithErrors/Procedures/csp_Test.sql", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Tests reference DatabaseLiteral.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseLiteral()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "SomeDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference DatabaseLiteral in new Format.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseLiteralNewFormat()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference DatabaseVariable.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseVariable()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbv=SomeDatabase")
                .AddSqlCmdVariables(new string[] { "SomeDatabase" })
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeDatabase)].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference ServerVariable and DatabaseVariable.
        /// </summary>
        [TestMethod]
        public void AddReferenceServerVariableAndDatabaseVariable()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbv=SomeDatabase|srv=SomeServer")
                .AddSqlCmdVariables(new string[] { "SomeDatabase", "SomeServer" })
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeServer)].[$(SomeDatabase)].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference ServerVariable and DatabaseLiteral.
        /// </summary>
        [TestMethod]
        public void AddReferenceServerVariableAndDatabaseLiteral()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase|srv=SomeServer")
                .AddSqlCmdVariables(new string[] { "SomeServer" })
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeServer)].[SomeDatabase].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }
    }
}
