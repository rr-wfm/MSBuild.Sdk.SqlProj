﻿using System;
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
        private readonly char[] separator = new[] { ';' };

        public PackageAnalyzer(IConsole console, string rulesExpression)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));

            BuildRuleLists(rulesExpression);
        }

        public void Analyze(TSqlModel model, FileInfo outputFile, FileInfo[] analyzers)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(outputFile);
            ArgumentNullException.ThrowIfNull(analyzers);

            _console.WriteLine($"Analyzing package '{outputFile.FullName}'");
            try
            {
                var factory = new CodeAnalysisServiceFactory();
                var settings = new CodeAnalysisServiceSettings();

                if (analyzers.Length == 0)
                {
                    // warning SR0016: Microsoft.Rules.Data : Stored procedure(sp_Test) includes sp_ prefix in its name.
                    _console.WriteLine("DacpacTool warning SQLPROJ0001: No additional rules files found, consider adding more rules via PackageReference - see the readme here: https://github.com/rr-wfm/MSBuild.Sdk.SqlProj.");
                }
                else
                {
                    _console.WriteLine("Loading analyzers: " + string.Join(", ", analyzers.Select(a => a.FullName)));
                    settings.AssemblyLookupPath = string.Join(';', analyzers.Select(a => a.DirectoryName));
                }

                var service = factory.CreateAnalysisService(model, settings);

                if (_ignoredRules.Count > 0 || _ignoredRuleSets.Count > 0)
                {
                    service.SetProblemSuppressor(p => 
                        _ignoredRules.Contains(p.Rule.RuleId) 
                        || _ignoredRuleSets.Any(s => p.Rule.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _console.WriteLine($"ERROR: An unknown error occurred while analyzing package '{outputFile.FullName}': {ex}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private void BuildRuleLists(string rulesExpression)
        {
            if (!string.IsNullOrWhiteSpace(rulesExpression))
            {
                foreach (var rule in rulesExpression.Split(separator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(rule => rule
                            .StartsWith('-')
                                && rule.Length > 1))
                {
                    if (rule.Length > 2 && rule.EndsWith('*'))
                    {
                        _ignoredRuleSets.Add(rule[1..^1]);
                    }
                    else
                    {
                        _ignoredRules.Add(rule[1..]);
                    }
                }
                foreach (var rule in rulesExpression.Split(separator,
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
