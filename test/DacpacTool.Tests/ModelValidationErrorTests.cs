using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class ModelValidationErrorTests
    {
        [TestMethod]
        public void Constructor_SetsProperties()
        {
            var error = new ModelValidationError(
                sourceName: "Tables/MyTable.sql",
                line: 3,
                column: 5,
                errorType: ModelErrorType.ParserError,
                severity: ModelErrorSeverity.Warning,
                prefix: "SQL",
                errorCode: 71501,
                message: "Something happened");

            error.SourceName.ShouldBe("Tables/MyTable.sql");
            error.Severity.ShouldBe(ModelErrorSeverity.Warning);
            error.ErrorCode.ShouldBe(71501);
        }

        [TestMethod]
        public void ToString_FormatsAllErrorParts()
        {
            var error = new ModelValidationError(
                sourceName: "Tables/MyTable.sql",
                line: 3,
                column: 5,
                errorType: ModelErrorType.ParserError,
                severity: ModelErrorSeverity.Warning,
                prefix: "SQL",
                errorCode: 71501,
                message: "Something happened");

            error.ToString().ShouldBe("Tables/MyTable.sql(3,5):ParserError Warning SQL71501: Something happened");
        }

        [TestMethod]
        public void GetOutputMessage_UsesRequestedSeverity()
        {
            var error = new ModelValidationError(
                sourceName: "Tables/MyTable.sql",
                line: 3,
                column: 5,
                errorType: ModelErrorType.ParserError,
                severity: ModelErrorSeverity.Warning,
                prefix: "SQL",
                errorCode: 71501,
                message: "Something happened");

            error.GetOutputMessage(ModelErrorSeverity.Error)
                .ShouldBe("Tables/MyTable.sql(3,5): Error SQL71501: Something happened");
        }
    }
}
