using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageAnalyzer
    {
        private readonly IConsole _console;
        private readonly HashSet<string> _ignoredRules = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignoredRuleSets = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _errorRuleSets = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _errorRulePrefixes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _suppressedProblemsByRule = new(StringComparer.Ordinal);
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

                _suppressedProblemsByRule.Clear();

                if (analyzers.Length > 0)
                {
                    settings.AssemblyLookupPath = string.Join(';', analyzers.Select(a => a.DirectoryName));
                }

                var service = factory.CreateAnalysisService(model, settings);

                var rules = service.GetRules();

                if (!rules.Any(r => r.Namespace == "SqlServer.Rules" || r.Namespace == "Smells"))
                {
                    _console.WriteLine("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the documentation here: https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/static-code-analysis.html. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
                }

                _console.WriteLine("Using analyzers: " + string.Join(", ", rules.Select(a => a.Namespace).Distinct()));

                var projectDir = Environment.CurrentDirectory;
                var suppressorPath = Path.Combine(projectDir, ProjectProblemSuppressor.SuppressionFilename);

                if (File.Exists(suppressorPath))
                {
                    _console.WriteLine($"Using suppressor file: {suppressorPath}");
                    var problemSuppressor = ProjectProblemSuppressor.CreateSuppressor(projectDir);
                    var suppressedProblems = problemSuppressor.GetSuppressedProblems();

                    foreach (var problem in suppressedProblems)
                    {
                        _console.WriteLine($"Suppressing rule: '{problem.Rule.RuleId}' in '{problem.SourceName}'");
                        var sourceName = Path.Combine(projectDir, problem.SourceName)
                            .Replace('\\', Path.AltDirectorySeparatorChar);

                        if (!_suppressedProblemsByRule.TryGetValue(problem.Rule.RuleId, out var sourceNames))
                        {
                            sourceNames = new HashSet<string>(StringComparer.Ordinal);
                            _suppressedProblemsByRule[problem.Rule.RuleId] = sourceNames;
                        }

                        sourceNames.Add(sourceName);
                    }
                }

                if (_ignoredRules.Count > 0 
                    || _ignoredRuleSets.Count > 0
                    || _suppressedProblemsByRule.Count > 0)
                {
                    service.SetProblemSuppressor(p =>
                        (_suppressedProblemsByRule.TryGetValue(p.Rule.RuleId, out var sourceNames) &&
                         sourceNames.Contains(p.Problem.SourceName.Replace('\\', Path.AltDirectorySeparatorChar)))
                        || _ignoredRules.Contains(p.Rule.RuleId)
                        || _ignoredRuleSets.Any(s => p.Rule.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
                }

                var result = service.Analyze(model);

                WriteAnalysisResults(
                    outputFile,
                    result.GetAllErrors().Select(err => err.GetOutputMessage()),
                    result.AnalysisSucceeded,
                    result.Problems.Select(err => err.GetOutputMessage(_errorRuleSets, _errorRulePrefixes)),
                    () => result.SerializeResultsToXml(GetOutputFileName(outputFile)));
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
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (rule.Length <= 1)
                    {
                        continue;
                    }

                    if (rule[0] == '-')
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
                    else if (rule.Length > 2 &&
                             rule.StartsWith("+!", StringComparison.OrdinalIgnoreCase))
                    {
                        if (rule.EndsWith('*'))
                        {
                            _errorRulePrefixes.Add(rule[2..^1]);
                        }
                        else
                        {
                            _errorRuleSets.Add(rule[2..]);
                        }
                    }
                }
            }
        }

        private void WriteAnalysisResults(FileInfo outputFile, IEnumerable<string> errors, bool analysisSucceeded, IEnumerable<string> problems, Action serializeResults)
        {
            ArgumentNullException.ThrowIfNull(outputFile);
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(problems);
            ArgumentNullException.ThrowIfNull(serializeResults);

            foreach (var error in errors)
            {
                _console.WriteLine(error);
            }

            if (!analysisSucceeded)
            {
                _console.WriteLine($"Analysis of package '{outputFile}' failed");
                return;
            }

            foreach (var problem in problems)
            {
                _console.WriteLine(problem);
            }

            serializeResults();
            _console.WriteLine($"Successfully analyzed package '{outputFile}'");
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
    }
}
