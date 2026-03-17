#nullable enable

using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimpleForeignKey
    {
        public string? Name { get; set; }

        public SimpleTable PrincipalTable { get; set; } = null!;

        public IList<SimpleColumn> Columns { get; set; } = [];

        public IList<SimpleColumn> PrincipalColumns { get; set; } = [];
    }
}
