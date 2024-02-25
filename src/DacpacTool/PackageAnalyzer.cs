using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using System.Linq;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageAnalyzer
    {
        private readonly IConsole _console;
        private readonly HashSet<string> _ignoredRules = new HashSet<string>();

        public PackageAnalyzer(IConsole console, string rulesExpression)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            if (!string.IsNullOrWhiteSpace(rulesExpression))
            {
                foreach (var rule in rulesExpression.Split(new[] { ';' }, 
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(rule => rule
                            .StartsWith("-", StringComparison.OrdinalIgnoreCase)
                                && rule.Length > 1))
                {
                    _ignoredRules.Add(rule.Substring(1));
                }
            }
        }

        public void Analyze(TSqlModel model, FileInfo outputFile)
        {
            _console.WriteLine($"Analyzing package '{outputFile.FullName}'");
            try
            {
                var factory = new CodeAnalysisServiceFactory();
                var service = factory.CreateAnalysisService(model);

                if (_ignoredRules.Count > 0)
                {
                    service.SetProblemSuppressor(p => _ignoredRules.Contains(p.Rule.RuleId));
                }

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
