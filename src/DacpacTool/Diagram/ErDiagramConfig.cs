#nullable enable

using System.IO;
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

            var config = JsonSerializer.Deserialize<ErDiagramConfig>(
                File.ReadAllText(file.FullName),
                SerializerOptions);

            return config ?? new ErDiagramConfig();
        }
    }
}
