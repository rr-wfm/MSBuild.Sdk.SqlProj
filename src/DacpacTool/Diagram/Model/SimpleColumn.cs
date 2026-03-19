#nullable enable

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimpleColumn
    {
        public string Name { get; set; } = string.Empty;

        public string? StoreType { get; set; }

        public bool IsNullable { get; set; }
    }
}
