using System;
using System.IO;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageAnalyzer
    {
        private readonly IConsole _console;

        public PackageAnalyzer(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public void Analyze(FileInfo outputFile, string suppressionsString = null)
        {
            _console.WriteLine($"Loading package '{outputFile.FullName}'");
            TSqlModel model;
            try
            {
                model = TSqlModel.LoadFromDacpac(
                        outputFile.FullName,
                        new ModelLoadOptions()
                        {
                            LoadAsScriptBackedModel = true,
                        });
            }
            catch (DacModelException ex)
            {
                _console.WriteLine($"ERROR: An unknown error occurred while loading package '{outputFile.FullName}': {ex.Message}");
                return;
            }

            _console.WriteLine($"Analyzing package '{outputFile.FullName}'");
            try
            {
                var factory = new CodeAnalysisServiceFactory();
                var service = factory.CreateAnalysisService(model);
                var result = service.Analyze(model);
                if (!result.AnalysisSucceeded)
                {
                    foreach (var err in result.InitializationErrors)
                    {
                        _console.WriteLine(err.GetOutputMessage());
                    }

                    foreach (var err in result.SuppressionErrors)
                    {
                        _console.WriteLine(err.GetOutputMessage());
                    }

                    foreach (var err in result.AnalysisErrors)
                    {
                        _console.WriteLine(err.GetOutputMessage());
                    }

                    return;
                }
                else
                {
                    foreach (var err in result.Problems)
                    {
                        _console.WriteLine(err.GetOutputMessage());
                    }

                    result.SerializeResultsToXml(GetOutputFileName(outputFile));
                }
                _console.WriteLine($"Successfully analyzed package '{outputFile}'");
            }
            catch (Exception ex)
            {
                _console.WriteLine($"ERROR: An unknown error occurred while analyzing package '{outputFile.FullName}': {ex.Message}");
            }
        }

        private string GetOutputFileName(FileInfo outputFile)
        {
            var outputFileName = outputFile.FullName;
            if (outputFile.Extension.Equals(".dacpac", StringComparison.OrdinalIgnoreCase))
            {
                outputFileName = outputFile.FullName.Substring(0, outputFile.FullName.Length - 7);
            }
            return outputFileName + ".CodeAnalysis.xml";
        }
    }
}
