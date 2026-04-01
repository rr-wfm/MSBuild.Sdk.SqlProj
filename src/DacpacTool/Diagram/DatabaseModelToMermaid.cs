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

        public string CreateMermaid()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("```mermaid");

            sb.AppendLine("erDiagram");

            foreach (var table in tables)
            {
                AddTable(sb, table);
            }

            sb.AppendLine("```");

            return sb.ToString();
        }

        private static void AddTable(System.Text.StringBuilder sb, SimpleTable table)
        {
            var formattedTableName = Sanitize(string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}");

            sb.AppendLine(CultureInfo.InvariantCulture, $"  {formattedTableName} {{");
            foreach (var column in table.Columns)
            {
                AddColum(sb, table, column);
            }

            sb.AppendLine("  }");

            foreach (var foreignKey in table.ForeignKeys)
            {
                var relationship = "}o--|";

                if (foreignKey.Columns.Any(c => c.IsNullable))
                {
                    relationship = "}o--o";
                }

                var formattedPrincipalTableName = Sanitize(string.IsNullOrEmpty(foreignKey.PrincipalTable.Schema) ? foreignKey.PrincipalTable.Name : $"{foreignKey.PrincipalTable.Schema}.{foreignKey.PrincipalTable.Name}");
                var formattedForeignKeyName = Sanitize(foreignKey.Name ?? string.Empty);

                sb.AppendLine(CultureInfo.InvariantCulture, $"  {formattedTableName} {relationship}| {formattedPrincipalTableName} : {formattedForeignKeyName}");
            }
        }

        private static void AddColum(System.Text.StringBuilder sb, SimpleTable table, SimpleColumn column)
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
