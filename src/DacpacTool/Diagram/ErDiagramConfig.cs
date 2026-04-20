#nullable enable

using System.IO;
using System.Linq;
using System.Text.Json;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used as a DTO for JSON configuration.")]
    public sealed class ErDiagramConfig
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public string? OutputFileName { get; init; }
        public string[]? Schemas { get; init; }
        public string[]? Tables { get; init; }

        public static ErDiagramConfig? Load(FileInfo? file)
        {
            if (file == null)
            {
                return null;
            }

            if (!file.Exists)
            {
                throw new FileNotFoundException($"ER diagram config file was not found: {file.FullName}", file.FullName);
            }

            var json = File.ReadAllText(file.FullName);
            using var document = JsonDocument.Parse(json);
            Validate(document.RootElement, file.FullName);

            var config = JsonSerializer.Deserialize<ErDiagramConfig>(json, SerializerOptions);

            return config ?? new ErDiagramConfig();
        }

        private static void Validate(JsonElement root, string fileName)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"ER diagram config '{fileName}' must contain a JSON object.");
            }

            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "$schema":
                        ValidateNonEmptyString(property.Value, fileName, property.Name);
                        break;
                    case "outputFileName":
                        ValidateNonEmptyString(property.Value, fileName, property.Name);
                        break;
                    case "schemas":
                    case "tables":
                        ValidateUniqueStringArray(property.Value, fileName, property.Name);
                        break;
                    default:
                        throw new JsonException($"ER diagram config '{fileName}' contains unsupported property '{property.Name}'.");
                }
            }
        }

        private static void ValidateNonEmptyString(JsonElement value, string fileName, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new JsonException($"ER diagram config '{fileName}' property '{propertyName}' must be a non-empty string.");
            }
        }

        private static void ValidateUniqueStringArray(JsonElement value, string fileName, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException($"ER diagram config '{fileName}' property '{propertyName}' must be an array of non-empty strings.");
            }

            var items = value.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    throw new JsonException($"ER diagram config '{fileName}' property '{propertyName}' must contain only non-empty strings.");
                }

                return item.GetString()!;
            }).ToArray();

            if (items.Distinct(System.StringComparer.Ordinal).Count() != items.Length)
            {
                throw new JsonException($"ER diagram config '{fileName}' property '{propertyName}' must not contain duplicate values.");
            }
        }
    }
}
