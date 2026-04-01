#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Diagram
{
    public class MermaidDiagramBuilder
    {
        private readonly IConsole _console;

        public MermaidDiagramBuilder(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public string? BuildErDiagram(TSqlModel model, string databaseName)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(model);

                var diagramFileName = $"{databaseName}_erdiagram.md";

                _console.WriteLine($"Generating ER diagram {diagramFileName}");

                var tables = DacpacModelFactory.GetTablesFromModel(model);

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
    }
}
