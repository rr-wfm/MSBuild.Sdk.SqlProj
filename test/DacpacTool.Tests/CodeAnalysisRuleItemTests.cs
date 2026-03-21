using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class CodeAnalysisRuleItemTests
    {
        [TestMethod]
        public void Build_TranslatesCodeAnalysisRuleItemsIntoEffectiveCodeAnalysisRules()
        {
            var rules = BuildProjectAndReadEffectiveRules("TestProjectWithCodeAnalysisRuleItems");

            rules.ShouldBe("-SqlServer.Rules.SRD*;+!SqlServer.Rules.SRN0005");
        }

        [TestMethod]
        public void Build_CombinesLegacyCodeAnalysisRulesWithCodeAnalysisRuleItems()
        {
            var rules = BuildProjectAndReadEffectiveRules("TestProjectWithCodeAnalysisRuleItemsAndProperty");

            rules.ShouldBe("-Smells.*;+!SqlServer.Rules.SRD0002;-SqlServer.Rules.SRD*;+!SqlServer.Rules.SRN0005");
        }

        [TestMethod]
        public void Build_CombinesGlobalWildcardErrorsWithSuppressedCodeAnalysisRuleItems()
        {
            var rules = BuildProjectAndReadEffectiveRules("TestProjectWithCodeAnalysisRuleItemsAndGlobalError");

            rules.ShouldBe("+!*;-SqlServer.Rules.SRD0004");
        }

        private static string BuildProjectAndReadEffectiveRules(string projectName)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var projectDirectory = Path.Combine(repoRoot, "test", projectName);
            var projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
            var outputPath = Path.Combine(Path.GetTempPath(), $"{projectName}.{Guid.NewGuid():N}.effective-code-analysis-rules.txt");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"msbuild \"{projectPath}\" /restore /nologo /t:WriteEffectiveCodeAnalysisRules /p:EffectiveCodeAnalysisRulesOutputFile=\"{outputPath}\"",
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            process.ExitCode.ShouldBe(0, standardOutput + Environment.NewLine + standardError);

            File.Exists(outputPath).ShouldBeTrue("Expected MSBuild fixture to write the effective code analysis rules file.");
            try
            {
                return File.ReadAllText(outputPath)
                    .Replace("\r\n", ";")
                    .Replace("\n", ";")
                    .Trim(';', ' ', '\r', '\n', '\t');
            }
            finally
            {
                File.Delete(outputPath);
            }
        }
    }
}
