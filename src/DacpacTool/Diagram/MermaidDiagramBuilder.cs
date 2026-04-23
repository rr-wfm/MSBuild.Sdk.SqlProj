#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Dac.Model;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram
{
    public class MermaidDiagramBuilder
    {
        private readonly IConsole _console;

        public MermaidDiagramBuilder(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public IReadOnlyList<string> BuildErDiagrams(TSqlModel model, string databaseName, IReadOnlyCollection<FileInfo>? erDiagramConfigs = null)
        {
            ArgumentNullException.ThrowIfNull(model);

            var diagramFiles = new List<string>();
            var configFiles = erDiagramConfigs?
                .Where(f => f != null)
                .ToList();

            if (configFiles == null || configFiles.Count == 0)
            {
                var defaultDiagram = BuildErDiagram(model, databaseName, null, $"{databaseName}_erdiagram.md");
                if (defaultDiagram != null)
                {
                    diagramFiles.Add(defaultDiagram);
                }

                return diagramFiles;
            }

            var useDefaultName = configFiles.Count == 1;
            foreach (var configFile in configFiles)
            {
                var config = ErDiagramConfig.Load(configFile);
                var diagramFileName = GetDiagramFileName(databaseName, configFile, config, useDefaultName);
                var diagram = BuildErDiagram(model, databaseName, config, diagramFileName);
                if (diagram != null)
                {
                    diagramFiles.Add(diagram);
                }
            }

            return diagramFiles;
        }

        public string? BuildErDiagram(TSqlModel model, string databaseName, FileInfo? erDiagramConfig = null)
        {
            ArgumentNullException.ThrowIfNull(model);

            var config = ErDiagramConfig.Load(erDiagramConfig);
            var diagramFileName = GetDiagramFileName(databaseName, erDiagramConfig, config, true);

            return BuildErDiagram(model, databaseName, config, diagramFileName);
        }

        private string? BuildErDiagram(TSqlModel model, string databaseName, ErDiagramConfig? config, string diagramFileName)
        {
            try
            {
                _console.WriteLine($"Generating ER diagram {diagramFileName}");

                var tables = DacpacModelFactory.GetTablesFromModel(model, config);
                var creator = new DatabaseModelToMermaid(tables.AsReadOnly());

                var diagram = creator.CreateMermaid();

                var fileName = Path.Combine(Environment.CurrentDirectory, diagramFileName);

                File.WriteAllText(fileName, diagram, Encoding.UTF8);

                return fileName;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _console.WriteLine("Error building ER diagram. Diagram generation will be skipped.");
                _console.WriteLine(ex.ToString());
            }
#pragma warning restore CA1031 // Do not catch general exception types

            return null;
        }

        private static string GetDiagramFileName(string databaseName, FileInfo? configFile, ErDiagramConfig? config, bool useDefaultName)
        {
            if (!string.IsNullOrWhiteSpace(config?.OutputFileName))
            {
                return config.OutputFileName;
            }

            if (useDefaultName || configFile == null)
            {
                return $"{databaseName}_erdiagram.md";
            }

            return $"{databaseName}_{Path.GetFileNameWithoutExtension(configFile.Name)}_erdiagram.md";
        }
    }
}
