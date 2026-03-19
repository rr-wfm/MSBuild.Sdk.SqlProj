using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram
{
    public class DatabaseModelToMermaid
    {
        private static readonly bool[] Lookup = new bool[70];
        private readonly IReadOnlyList<SimpleTable> tables;

        static DatabaseModelToMermaid()
        {
            for (char c = '0'; c <= '9'; c++)
            {
                Lookup[c] = true;
            }

            for (char c = 'A'; c <= 'Z'; c++)
            {
                Lookup[c] = true;
            }

            for (char c = 'a'; c <= 'z'; c++)
            {
                Lookup[c] = true;
            }

            Lookup['.'] = true;
            Lookup['_'] = true;
            Lookup['-'] = true;
        }

        public DatabaseModelToMermaid(IReadOnlyList<SimpleTable> tables)
        {
            this.tables = tables;
        }

        public string CreateMermaid()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("```mermaid");

            sb.AppendLine("erDiagram");

            foreach (var table in tables)
            {
                var formattedTableName = Sanitize(table.Name);

                sb.AppendLine(CultureInfo.InvariantCulture, $"  {formattedTableName} {{");
                foreach (var column in table.Columns)
                {
                    var formattedColumnName = Sanitize(column.Name);

                    var pkfk = string.Empty;

                    if (table.PrimaryKey?.Columns.Contains(column) ?? false)
                    {
                        pkfk = "PK";
                    }

                    if (table.ForeignKeys.Any(c => c.Columns.Contains(column)))
                    {
                        pkfk = string.IsNullOrEmpty(pkfk) ? "FK" : "PK,FK";
                    }

                    var nullable = column.IsNullable ? "(NULL)" : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {formattedColumnName} {column.StoreType?.Replace(", ", "-", StringComparison.OrdinalIgnoreCase)}{nullable} {pkfk}");
                }

                sb.AppendLine("  }");

                foreach (var foreignKey in table.ForeignKeys)
                {
                    var relationship = "}o--|";

                    if (foreignKey.Columns.Any(c => c.IsNullable))
                    {
                        relationship = "}o--o";
                    }

                    var formattedPrincipalTableName = Sanitize(foreignKey.PrincipalTable.Name);
                    var formattedForeignKeyName = Sanitize(foreignKey.Name ?? string.Empty);

                    sb.AppendLine(CultureInfo.InvariantCulture, $"  {formattedTableName} {relationship}| {formattedPrincipalTableName} : {formattedForeignKeyName}");
                }
            }

            sb.AppendLine("```");

            return sb.ToString();
        }

        private static string Sanitize(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                char[] buffer = new char[name.Length];
                int index = 0;
                foreach (char c in name)
                {
                    if (Lookup[c])
                    {
                        buffer[index] = c;
                        index++;
                    }
                }

                return new string(buffer, 0, index);
            }

            return name;
        }
    }
}
