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
            error.ToString().ShouldBe("../../../../TestProjectWithErrors/Procedures/csp_Test.sql(2,18):ModelValidationError Error SQL71501: SqlSubroutineParameter: [dbo].[csp_Test].[@p_Parameter] has an unresolved reference to SqlBuiltInType [dbo].[MyCustomType].");
        }
    }
}
