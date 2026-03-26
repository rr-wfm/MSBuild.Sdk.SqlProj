#nullable enable

using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model
{
    public class SimpleForeignKey
    {
        public string? Name { get; set; }

        public required SimpleTable PrincipalTable { get; init; }

        public IList<SimpleColumn> Columns { get; init; } = [];

        public IList<SimpleColumn> PrincipalColumns { get; init; } = [];
    }
}
