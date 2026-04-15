#nullable enable
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
            var formattedTableName = Sanitize($"{table.Schema}.{table.Name}");

            sb.AppendLine(CultureInfo.InvariantCulture, $"  \"{formattedTableName}\" {{");
            foreach (var column in table.Columns)
            {
                AddColumn(sb, table, column);
            }

            sb.AppendLine("  }");

            foreach (var foreignKey in table.ForeignKeys)
            {
                var relationship = "}o--|";

                if (foreignKey.Columns.Any(c => c.IsNullable))
                {
                    relationship = "}o--o";
                }

                var formattedPrincipalTableName = Sanitize($"{foreignKey.PrincipalTable.Schema}.{foreignKey.PrincipalTable.Name}");
                var formattedForeignKeyName = Sanitize(foreignKey.Name ?? string.Empty);

                sb.AppendLine(CultureInfo.InvariantCulture, $"  \"{formattedTableName}\" {relationship}| \"{formattedPrincipalTableName}\" : {formattedForeignKeyName}");
            }
        }

        private static void AddColumn(System.Text.StringBuilder sb, SimpleTable table, SimpleColumn column)
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

            var storeType = SanitizeStoreType(column.StoreType ?? string.Empty);

            sb.AppendLine(CultureInfo.InvariantCulture, $"    {formattedColumnName} {storeType}{nullable} {pkfk}");
        }

        private static string SanitizeStoreType(string storeType)
        {
            if (string.IsNullOrEmpty(storeType))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(storeType.Length);
            var lastWasSeparator = false;

            foreach (var c in storeType)
            {
                if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '(' or ')')
                {
                    sb.Append(c);
                    lastWasSeparator = false;
                    continue;
                }

                if (char.IsWhiteSpace(c) || c is ',' or '*' or '+' or '/' or '=' or ':')
                {
                    if (!lastWasSeparator)
                    {
                        sb.Append('-');
                        lastWasSeparator = true;
                    }
                }
            }

            return sb.ToString().Trim('-');
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
