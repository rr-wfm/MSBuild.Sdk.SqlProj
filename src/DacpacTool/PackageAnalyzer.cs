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
        private readonly HashSet<string> _ignoredRules = new();
        private readonly HashSet<string> _ignoredRuleSets = new();
        private readonly HashSet<string> _errorRuleSets = new();

        public PackageAnalyzer(IConsole console, string rulesExpression)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            BuildRuleLists(rulesExpression);
        }

        public void AddRulesFile(FileInfo inputFile)
        {
            // Make sure the file exists
            if (!inputFile.Exists)
            {
                throw new ArgumentException($"Unable to find rules file {inputFile}", nameof(inputFile));
            }

            if (inputFile.Directory.Name.Equals("rules", StringComparison.OrdinalIgnoreCase)
                && inputFile.Extension.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                CopyAdditionalRulesFile(inputFile);
            }
        }

        public void Analyze(TSqlModel model, FileInfo outputFile)
        {
            _console.WriteLine($"Analyzing package '{outputFile.FullName}'");
            try
            {
                var factory = new CodeAnalysisServiceFactory();
                var service = factory.CreateAnalysisService(model);

                if (_ignoredRules.Count > 0 || _ignoredRuleSets.Count > 0)
                {
                    service.SetProblemSuppressor(p => 
                        _ignoredRules.Contains(p.Rule.RuleId) 
                        || _ignoredRuleSets.Any(s => p.Rule.RuleId.StartsWith(s)));
                }

                var result = service.Analyze(model);
                if (!result.AnalysisSucceeded)
                {
                    var errors = result.GetAllErrors();
                    foreach (var err in errors)
                    {
                        _console.WriteLine(err.GetOutputMessage());
                    }
                    return;
                }
                else
                {
                    foreach (var err in result.Problems)
                    {
                        _console.WriteLine(err.GetOutputMessage(_errorRuleSets));
                    }

                    result.SerializeResultsToXml(GetOutputFileName(outputFile));
                }
                _console.WriteLine($"Successfully analyzed package '{outputFile}'");
            }
            catch (Exception ex)
            {
                _console.WriteLine($"ERROR: An unknown error occurred while analyzing package '{outputFile.FullName}': {ex}");
            }
        }

        private void BuildRuleLists(string rulesExpression)
        {
            if (!string.IsNullOrWhiteSpace(rulesExpression))
            {
                foreach (var rule in rulesExpression.Split(new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(rule => rule
                            .StartsWith("-", StringComparison.OrdinalIgnoreCase)
                                && rule.Length > 1))
                {
                    if (rule.Length > 2 && rule.EndsWith("*", StringComparison.OrdinalIgnoreCase))
                    {
                        _ignoredRuleSets.Add(rule[1..^1]);
                    }
                    else
                    {
                        _ignoredRules.Add(rule[1..]);
                    }
                }
                foreach (var rule in rulesExpression.Split(new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(rule => rule
                            .StartsWith("+!", StringComparison.OrdinalIgnoreCase)
                                && rule.Length > 2))
                {
                    _errorRuleSets.Add(rule[2..]);
                }
            }
        }

        private static string GetOutputFileName(FileInfo outputFile)
        {
            var outputFileName = outputFile.FullName;
            if (outputFile.Extension.Equals(".dacpac", StringComparison.OrdinalIgnoreCase))
            {
                outputFileName = outputFile.FullName[..^7];
            }
            return outputFileName + ".CodeAnalysis.xml";
        }

        private void CopyAdditionalRulesFile(FileInfo rulesFile)
        {
            var destPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var dest = Path.Combine(destPath, rulesFile.Name);
                    
            rulesFile.CopyTo(dest, overwrite: true);

            _console.WriteLine($"Adding additional rules file from '{rulesFile.FullName}' to '{dest}'");
        }
    }
}
