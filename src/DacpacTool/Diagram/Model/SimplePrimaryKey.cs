#nullable enable

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimplePrimaryKey
    {
        public string? Name { get; set; }

        public IList<SimpleColumn> Columns { get; init; } = [];
    }
}
