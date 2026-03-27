using System;
using Microsoft.SqlServer.Dac.Extensibility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class ExtensibilityErrorExtensionsTests
    {
        [TestMethod]
        public void GetOutputMessage_Null_Throws()
        {
            Should.Throw<ArgumentNullException>(() => ExtensibilityErrorExtensions.GetOutputMessage(null!));
        }

        [TestMethod]
        public void GetOutputMessage_FormatsMessage()
        {
            var error = new ExtensibilityError("Contributor message", Severity.Message)
            {
                Document = "contributor.sql",
                Line = 4,
                Column = 2,
            };

            var message = error.GetOutputMessage();

            message.ShouldBe("contributor.sql(4,2): Message 0: Contributor message");
        }
    }
}
