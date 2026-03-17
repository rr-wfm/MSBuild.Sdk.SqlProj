#nullable enable

using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimplePrimaryKey
    {
        public string? Name { get; set; }

        public IList<SimpleColumn> Columns { get; set; } = [];
    }
}
