using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram
{
    public class DatabaseModelToMermaid
    {
        private readonly IReadOnlyList<SimpleTable> tables;

        public DatabaseModelToMermaid(IReadOnlyList<SimpleTable> tables)
        {
            this.tables = tables;
        }

        private static bool IsValidChar(char c) =>
            c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '.' or '_' or '-';

        public string CreateMermaid(string databaseName)
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
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            Span<char> buffer = new char[name.Length];

            int index = 0;
            foreach (char c in name)
            {
                if (IsValidChar(c))
                {
                    buffer[index++] = c;
                }
            }

            return new string(buffer[..index]);
        }
    }
}
