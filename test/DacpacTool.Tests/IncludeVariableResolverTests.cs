using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class IncludeVariableResolverTests
    {
        [TestMethod]
        public void SetVariable_StoresValueThatCanBeReadBack()
        {
            var resolver = new IncludeVariableResolver();
            var position = new PositionStruct();

            resolver.SetVariable(position, "MyVariable", "MyValue");

            resolver.GetVariable(position, "MyVariable").ShouldBe("MyValue");
        }
    }
}
