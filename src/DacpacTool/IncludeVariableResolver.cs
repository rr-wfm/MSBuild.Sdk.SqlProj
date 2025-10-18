using System.Globalization;
using System.Text;
using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool;

// This was just copied from Microsoft.SqlTools.ManagedBatchParser.UnitTests.BatchParser.TestVariableResolver
// No special functionality needed for our project
internal sealed class IncludeVariableResolver : IVariableResolver
{
    private StringBuilder outputString;
    private BatchParserSqlCmd batchParserSqlCmd;

    public IncludeVariableResolver()
    {
        this.outputString = new StringBuilder();
        batchParserSqlCmd = new BatchParserSqlCmd();
    }

    public string GetVariable(PositionStruct pos, string name)
    {
        return batchParserSqlCmd.GetVariable(pos, name);
    }

    public void SetVariable(PositionStruct pos, string name, string value)
    {
        outputString.AppendFormat(CultureInfo.InvariantCulture, "Setting variable {0} to [{1}]\n", name, value);
        batchParserSqlCmd.SetVariable(pos, name, value);
    }
}
