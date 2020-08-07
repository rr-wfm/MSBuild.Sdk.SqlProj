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
    }
}
