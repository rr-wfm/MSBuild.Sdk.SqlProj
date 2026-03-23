#nullable enable

using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimpleTable
    {
        public string Name { get; set; } = string.Empty;

        public string Schema { get; set; } = string.Empty;

        public IList<SimpleColumn> Columns { get; init; } = [];

        public SimplePrimaryKey? PrimaryKey { get; set; }

        public IList<SimpleForeignKey> ForeignKeys { get; init; } = [];
    }
}
