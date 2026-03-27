using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac.CodeAnalysis;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageAnalyzer
    {
        private const string EditorConfigFileName = ".editorconfig";
        private const string DotNetDiagnosticPrefix = "dotnet_diagnostic.";
        private const string SeveritySuffix = ".severity";
        private readonly IConsole _console;
        private readonly HashSet<string> _ignoredRules = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignoredRuleSets = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _errorRuleSets = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _rulesExpression;
        private readonly char[] separator = [';'];

        public PackageAnalyzer(IConsole console, string rulesExpression)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
            _rulesExpression = rulesExpression;
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

                if (analyzers.Length > 0)
                {
                    settings.AssemblyLookupPath = string.Join(';', analyzers.Select(a => a.DirectoryName));
                }

                var service = factory.CreateAnalysisService(model, settings);

                var rules = service.GetRules();

                if (!rules.Any(r => r.Namespace == "SqlServer.Rules" || r.Namespace == "Smells"))
                {
                    _console.WriteLine("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the readme here: https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/blob/master/README.md#static-code-analysis. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
                }

                _console.WriteLine("Using analyzers: " + string.Join(", ", rules.Select(a => a.Namespace).Distinct()));

                var projectDir = Environment.CurrentDirectory;
                BuildRuleLists(projectDir, _rulesExpression);
                var suppressorPath = Path.Combine(projectDir, ProjectProblemSuppressor.SuppressionFilename);
                List<SuppressedProblemInfo> suppressedProblems = new();

                if (File.Exists(suppressorPath))
                {
                    _console.WriteLine($"Using suppressor file: {suppressorPath}");
                    var problemSuppressor = ProjectProblemSuppressor.CreateSuppressor(projectDir);

                    suppressedProblems = problemSuppressor.GetSuppressedProblems().ToList();

                    foreach (var problem in suppressedProblems)
                    {
                        _console.WriteLine($"Suppressing rule: '{problem.Rule.RuleId}' in '{problem.SourceName}'");
                    }
                }

                var suppressedProblemKeys = suppressedProblems
                    .Select(problem => BuildSuppressedProblemKey(projectDir, problem.Rule.RuleId, problem.SourceName))
                    .ToHashSet(StringComparer.Ordinal);

                if (_ignoredRules.Count > 0 
                    || _ignoredRuleSets.Count > 0
                    || suppressedProblems.Count > 0)
                {
                    service.SetProblemSuppressor(problem =>
                        suppressedProblemKeys.Contains(BuildSuppressedProblemKey(projectDir, problem.Rule.RuleId, problem.Problem.SourceName))
                        || _ignoredRules.Contains(problem.Rule.RuleId)
                        || _ignoredRuleSets.Any(s => problem.Rule.RuleId.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
                }

                var result = service.Analyze(model);

                var errors = result.GetAllErrors();
                foreach (var err in errors)
                {
                    _console.WriteLine(err.GetOutputMessage());
                }

                if (!result.AnalysisSucceeded)
                {
                    _console.WriteLine($"Analysis of package '{outputFile}' failed");
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

        private void BuildRuleLists(string projectDir, string rulesExpression)
        {
            _ignoredRules.Clear();
            _ignoredRuleSets.Clear();
            _errorRuleSets.Clear();

            var exactRuleActions = new Dictionary<string, RuleAction>(StringComparer.OrdinalIgnoreCase);
            var prefixedSuppressions = new Dictionary<string, RuleAction>(StringComparer.OrdinalIgnoreCase);
            var errorPatterns = new Dictionary<string, RuleAction>(StringComparer.OrdinalIgnoreCase);

            foreach (var expression in LoadRuleExpressionsFromEditorConfig(projectDir))
            {
                ApplyRuleExpression(expression, exactRuleActions, prefixedSuppressions, errorPatterns);
            }

            if (!string.IsNullOrWhiteSpace(rulesExpression))
            {
                foreach (var expression in rulesExpression.Split(separator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ApplyRuleExpression(expression, exactRuleActions, prefixedSuppressions, errorPatterns);
                }
            }

            foreach (var exactRuleAction in exactRuleActions)
            {
                if (exactRuleAction.Value == RuleAction.Suppress)
                {
                    _ignoredRules.Add(exactRuleAction.Key);
                }
                else
                {
                    _errorRuleSets.Add(exactRuleAction.Key);
                }
            }

            foreach (var prefixedSuppression in prefixedSuppressions.Where(p => p.Value == RuleAction.Suppress))
            {
                _ignoredRuleSets.Add(prefixedSuppression.Key);
            }

            foreach (var errorPattern in errorPatterns.Where(p => p.Value == RuleAction.Error))
            {
                _errorRuleSets.Add(errorPattern.Key);
            }
        }

        private static void ApplyRuleExpression(
            string expression,
            IDictionary<string, RuleAction> exactRuleActions,
            IDictionary<string, RuleAction> prefixedSuppressions,
            IDictionary<string, RuleAction> errorPatterns)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            if (expression.Length > 1 && expression[0] == '-')
            {
                if (expression.Length > 2 && expression.EndsWith('*'))
                {
                    prefixedSuppressions[expression[1..^1]] = RuleAction.Suppress;
                }
                else
                {
                    exactRuleActions[expression[1..]] = RuleAction.Suppress;
                }

                return;
            }

            if (expression.StartsWith("+!", StringComparison.OrdinalIgnoreCase) && expression.Length > 2)
            {
                var errorPattern = expression[2..];
                if (errorPattern.EndsWith('*'))
                {
                    errorPatterns[errorPattern] = RuleAction.Error;
                }
                else
                {
                    exactRuleActions[errorPattern] = RuleAction.Error;
                }
            }
        }

        private IEnumerable<string> LoadRuleExpressionsFromEditorConfig(string projectDir)
        {
            if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            {
                yield break;
            }

            foreach (var severityOverride in LoadEditorConfigSeverityOverrides(projectDir))
            {
                if (!TryNormalizeEditorConfigRuleId(severityOverride.Key, out var normalizedRuleId))
                {
                    _console.WriteLine($"DacpacTool warning SQLPROJ0002: .editorconfig rule id '{severityOverride.Key}' must use a fully qualified rule name in the form '<AnalyzerNamespace>.<RuleId>'.");
                    continue;
                }

                if (severityOverride.Value == RuleAction.Suppress)
                {
                    yield return "-" + normalizedRuleId;
                }
                else if (severityOverride.Value == RuleAction.Error)
                {
                    yield return "+!" + normalizedRuleId;
                }
            }
        }

        private static Dictionary<string, RuleAction> LoadEditorConfigSeverityOverrides(string projectDir)
        {
            var severityOverrides = new Dictionary<string, RuleAction>(StringComparer.OrdinalIgnoreCase);
            var sqlFilePaths = EnumerateSqlFilePaths(projectDir);

            foreach (var editorConfigPath in EnumerateEditorConfigFiles(projectDir))
            {
                MergeEditorConfigFile(editorConfigPath, projectDir, sqlFilePaths, severityOverrides);
            }

            return severityOverrides;
        }

        private static List<string> EnumerateSqlFilePaths(string projectDir)
        {
            if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            {
                return [];
            }

            return Directory.EnumerateFiles(projectDir, "*.sql", SearchOption.AllDirectories)
                .Select(filePath => Path.GetRelativePath(projectDir, filePath).Replace('\\', '/'))
                .ToList();
        }

        private static Stack<string> EnumerateEditorConfigFiles(string projectDir)
        {
            var paths = new Stack<string>();
            var currentDirectory = new DirectoryInfo(projectDir);

            while (currentDirectory is not null)
            {
                var editorConfigPath = Path.Combine(currentDirectory.FullName, EditorConfigFileName);
                if (File.Exists(editorConfigPath))
                {
                    paths.Push(editorConfigPath);
                    if (HasRootSetting(editorConfigPath))
                    {
                        break;
                    }
                }

                currentDirectory = currentDirectory.Parent;
            }

            return paths;
        }

        private static bool HasRootSetting(string editorConfigPath)
        {
            foreach (var line in File.ReadLines(editorConfigPath))
            {
                var trimmedLine = line.Trim();
                if (IsCommentOrEmpty(trimmedLine) || trimmedLine.StartsWith('['))
                {
                    continue;
                }

                var separatorIndex = trimmedLine.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = trimmedLine[..separatorIndex].Trim();
                var value = trimmedLine[(separatorIndex + 1)..].Trim();
                if (key.Equals("root", StringComparison.OrdinalIgnoreCase)
                    && value.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MergeEditorConfigFile(
            string editorConfigPath,
            string projectDir,
            IReadOnlyCollection<string> sqlFilePaths,
            IDictionary<string, RuleAction> severityOverrides)
        {
            var sectionApplies = true;
            var editorConfigDirectory = Path.GetDirectoryName(editorConfigPath) ?? projectDir;

            foreach (var line in File.ReadLines(editorConfigPath))
            {
                var trimmedLine = line.Trim();
                if (IsCommentOrEmpty(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    sectionApplies = SectionAppliesToSqlFiles(editorConfigDirectory, projectDir, sqlFilePaths, trimmedLine[1..^1].Trim());
                    continue;
                }

                if (!sectionApplies)
                {
                    continue;
                }

                var separatorIndex = trimmedLine.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = trimmedLine[..separatorIndex].Trim();
                var value = trimmedLine[(separatorIndex + 1)..].Trim();
                if (!TryMapSeverityOverride(key, value, out var ruleId, out var ruleAction))
                {
                    continue;
                }

                severityOverrides[ruleId] = ruleAction;
            }
        }

        private static bool TryMapSeverityOverride(string key, string value, out string ruleId, out RuleAction ruleAction)
        {
            ruleId = null;
            ruleAction = default;

            if (!key.StartsWith(DotNetDiagnosticPrefix, StringComparison.OrdinalIgnoreCase)
                || !key.EndsWith(SeveritySuffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var ruleIdLength = key.Length - DotNetDiagnosticPrefix.Length - SeveritySuffix.Length;
            if (ruleIdLength <= 0)
            {
                return false;
            }

            ruleId = key.Substring(DotNetDiagnosticPrefix.Length, ruleIdLength);
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                ruleAction = RuleAction.Suppress;
                return true;
            }

            if (value.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                ruleAction = RuleAction.Error;
                return true;
            }

            return false;
        }

        private static bool TryNormalizeEditorConfigRuleId(string ruleId, out string normalizedRuleId)
        {
            normalizedRuleId = null;
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return false;
            }

            if (!ruleId.Contains('.', StringComparison.Ordinal))
            {
                return false;
            }

            normalizedRuleId = ruleId;
            return true;
        }

        private static bool SectionAppliesToSqlFiles(
            string editorConfigDirectory,
            string projectDir,
            IReadOnlyCollection<string> sqlFilePaths,
            string sectionPattern)
        {
            if (string.IsNullOrWhiteSpace(sectionPattern) || sqlFilePaths.Count == 0)
            {
                return false;
            }

            var normalizedPattern = sectionPattern.Replace('\\', '/');
            var editorConfigRelativeDirectory = Path.GetRelativePath(projectDir, editorConfigDirectory).Replace('\\', '/');
            if (editorConfigRelativeDirectory == ".")
            {
                editorConfigRelativeDirectory = string.Empty;
            }

            return sqlFilePaths.Any(sqlFilePath => PatternMatchesSqlFile(normalizedPattern, editorConfigRelativeDirectory, sqlFilePath));
        }

        private static bool PatternMatchesSqlFile(string sectionPattern, string editorConfigRelativeDirectory, string sqlFilePath)
        {
            var candidatePath = string.IsNullOrEmpty(editorConfigRelativeDirectory)
                ? sqlFilePath
                : $"{editorConfigRelativeDirectory.TrimEnd('/')}/{sqlFilePath}";

            return IsEditorConfigMatch(sectionPattern, candidatePath)
                || (!sectionPattern.Contains('/', StringComparison.Ordinal) && IsEditorConfigMatch(sectionPattern, Path.GetFileName(sqlFilePath)));
        }

        private static bool IsEditorConfigMatch(string pattern, string candidate)
        {
            var regexPattern = "^" + ConvertEditorConfigPatternToRegex(pattern) + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(candidate, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static string ConvertEditorConfigPatternToRegex(string pattern)
        {
            var builder = new System.Text.StringBuilder();

            for (var index = 0; index < pattern.Length; index++)
            {
                var current = pattern[index];
                if (current == '*')
                {
                    var isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                    if (isDoubleStar)
                    {
                        builder.Append(".*");
                        index++;
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }

                    continue;
                }

                if (current == '?')
                {
                    builder.Append("[^/]");
                    continue;
                }

                if ("+()^$.{}[]|\\".Contains(current, StringComparison.Ordinal))
                {
                    builder.Append('\\');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsCommentOrEmpty(string line) =>
            string.IsNullOrWhiteSpace(line)
            || line.StartsWith('#')
            || line.StartsWith(';');

        private static string BuildSuppressedProblemKey(string projectDir, string ruleId, string sourceName) =>
            $"{ruleId}|{NormalizeSourcePath(projectDir, sourceName)}";

        private static string NormalizeSourcePath(string projectDir, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return string.Empty;
            }

            return Path.Combine(projectDir, sourceName)
                .Replace('\\', Path.AltDirectorySeparatorChar);
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

        private enum RuleAction
        {
            Suppress,
            Error,
        }
    }
}
