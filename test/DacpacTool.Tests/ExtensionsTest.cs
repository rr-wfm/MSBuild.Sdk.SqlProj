using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

        [TestMethod]
        public void AddReferenceDatabaseLiteralAndVariable_UsesLiteralValue()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase|dbv=WrongDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        [TestMethod]
        public void ParseExternalParts_WhenRegexTimesOut_ShouldThrowArgumentException()
        {
            // Arrange
            // Use a regex designed to trigger catastrophic backtracking so the timeout path is deterministic in test.
            var regex = new Regex("dbl=(?<dbl>(a+)+)$", RegexOptions.None, TimeSpan.FromMilliseconds(1));
            var method = typeof(Extensions).GetMethod("ParseExternalParts", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(Regex) }, null);

            // Act
            // The trailing '!' prevents a full match and forces catastrophic backtracking in the injected regex.
            Action action = () => method!.Invoke(null, new object[] { "dbl=" + new string('a', 2048) + "!", regex });

            // Assert
            var exception = action.ShouldThrow<TargetInvocationException>().InnerException.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("externalParts");
            exception.Message.ShouldBe(
                "Unable to parse reference external parts. " +
                "Use a database literal or SQLCMD variable metadata such as " +
                "'DatabaseVariableLiteralValue=\"MyDatabase\"', " +
                "'DatabaseSqlCmdVariable=\"MyDatabaseVar\"', or " +
                "'DatabaseSqlCmdVariable=\"MyDatabaseVar\" ServerSqlCmdVariable=\"MyServerVar\"'. " +
                "(Parameter 'externalParts')");
            exception.InnerException.ShouldBeOfType<RegexMatchTimeoutException>();
        }
    }
}
