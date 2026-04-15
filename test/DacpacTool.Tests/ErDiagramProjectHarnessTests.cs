using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ErDiagramProjectHarnessTests
    {
        private static readonly string ProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../ErDiagramHarnessProject"));
        private static readonly string ProjectFile = Path.Combine(ProjectDirectory, "ErDiagramHarnessProject.csproj");
        private static readonly string ExpectedDirectory = Path.Combine(ProjectDirectory, "Expected");
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly string[] DacpacToolTargetFrameworks = { "net8.0", "net9.0", "net10.0" };
        private static readonly string[] BuildConfigurations = { "Debug", "Release" };
        private static readonly string[] GeneratedDiagramFiles =
        {
            "sales_erdiagram.md",
            "hr_erdiagram.md",
            "special_erdiagram.md",
            "computed-column_erdiagram.md",
        };

        [TestMethod]
        public void BuildProject_GeneratesExpectedDiagrams()
        {
            CleanupGeneratedFiles();

            try
            {
                var build = BuildHarnessProject();
                build.ExitCode.ShouldBe(0, build.Output);

                foreach (var diagramFile in GeneratedDiagramFiles)
                {
                    var actualPath = Path.Combine(ProjectDirectory, diagramFile);
                    var expectedPath = Path.Combine(ExpectedDirectory, diagramFile);

                    File.Exists(actualPath).ShouldBeTrue();
                    File.Exists(expectedPath).ShouldBeTrue();

                    var actual = NormalizeLineEndings(File.ReadAllText(actualPath, Encoding.UTF8));
                    var expected = NormalizeLineEndings(File.ReadAllText(expectedPath, Encoding.UTF8));

                    actual.ShouldBe(expected, $"Expected SHA256 {ComputeSha256(expected)}, actual SHA256 {ComputeSha256(actual)}");
                }
            }
            finally
            {
                CleanupGeneratedFiles();
            }
        }

        [TestMethod]
        public void BuildProject_GeneratedDiagramsParseWithMermaidCli_WhenAvailable()
        {
            CleanupGeneratedFiles();

            try
            {
                var build = BuildHarnessProject();
                build.ExitCode.ShouldBe(0, build.Output);

                if (!CanRunProcess("mmdc", "-h"))
                {
                    Assert.Inconclusive("Mermaid CLI was not found on PATH. Install `mmdc` to enable parser validation.");
                }

                foreach (var diagramFile in GeneratedDiagramFiles)
                {
                    var markdownPath = Path.Combine(ProjectDirectory, diagramFile);
                    var mermaidSourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mmd");
                    var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
                    var puppeteerConfigPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

                    try
                    {
                        File.WriteAllText(mermaidSourcePath, ExtractMermaidBlock(File.ReadAllText(markdownPath, Encoding.UTF8)), Encoding.UTF8);
                        File.WriteAllText(puppeteerConfigPath, "{\"args\":[\"--no-sandbox\"]}", Utf8WithoutBom);

                        var render = RunProcess("mmdc", $"-p \"{puppeteerConfigPath}\" -i \"{mermaidSourcePath}\" -o \"{outputPath}\"");
                        if (IsMermaidCliEnvironmentFailure(render))
                        {
                            Assert.Inconclusive("Mermaid CLI is installed, but the browser runtime could not start in this environment.");
                        }

                        render.ExitCode.ShouldBe(0, render.Output);
                        File.Exists(outputPath).ShouldBeTrue();
                    }
                    finally
                    {
                        if (File.Exists(mermaidSourcePath))
                        {
                            File.Delete(mermaidSourcePath);
                        }

                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                        }

                        if (File.Exists(puppeteerConfigPath))
                        {
                            File.Delete(puppeteerConfigPath);
                        }
                    }
                }
            }
            finally
            {
                CleanupGeneratedFiles();
            }
        }

        private static void CleanupGeneratedFiles()
        {
            foreach (var diagramPath in GeneratedDiagramFiles.Select(file => Path.Combine(ProjectDirectory, file)))
            {
                if (File.Exists(diagramPath))
                {
                    File.Delete(diagramPath);
                }
            }
        }

        private static (int ExitCode, string Output) RunProcess(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = ProjectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, NormalizeLineEndings(standardOutput + standardError));
        }

        private static (int ExitCode, string Output) BuildHarnessProject()
        {
            var dacpacToolExe = GetDacpacToolExe();

            File.Exists(dacpacToolExe).ShouldBeTrue($"Expected test-built DacpacTool at {dacpacToolExe}");
            return RunProcess("dotnet", $"build \"{ProjectFile}\" -nologo -t:Rebuild -p:DacpacToolExe=\"{dacpacToolExe}\"");
        }

        private static bool CanRunProcess(string fileName, string arguments)
        {
            try
            {
                var result = RunProcess(fileName, arguments);
                return result.ExitCode == 0;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                return false;
            }
        }

        private static bool IsMermaidCliEnvironmentFailure((int ExitCode, string Output) result)
        {
            return result.ExitCode != 0
                && (result.Output.Contains("Failed to launch the browser process", StringComparison.Ordinal)
                    || result.Output.Contains("No usable sandbox", StringComparison.Ordinal));
        }

        private static string ExtractMermaidBlock(string markdown)
        {
            var normalized = NormalizeLineEndings(markdown);
            const string openingFence = "```mermaid\n";
            const string closingFence = "\n```";

            var startIndex = normalized.IndexOf(openingFence, StringComparison.Ordinal);
            startIndex.ShouldBeGreaterThanOrEqualTo(0);

            startIndex += openingFence.Length;
            var endIndex = normalized.IndexOf(closingFence, startIndex, StringComparison.Ordinal);
            endIndex.ShouldBeGreaterThan(startIndex);

            return normalized[startIndex..endIndex];
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string GetDacpacToolExe()
        {
            var preferredTargetFramework = GetCurrentTargetFrameworkMoniker();
            foreach (var candidatePath in GetDacpacToolCandidatePaths(preferredTargetFramework))
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "../../../../../src/DacpacTool/bin/Release",
                    preferredTargetFramework,
                    "DacpacTool.dll"));
        }

        private static string GetCurrentTargetFrameworkMoniker()
        {
            var targetFrameworkAttribute = typeof(ErDiagramProjectHarnessTests).Assembly
                .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>()
                .FirstOrDefault();
            var frameworkName = targetFrameworkAttribute?.FrameworkName ?? string.Empty;
            if (frameworkName.Contains("net8.0", StringComparison.OrdinalIgnoreCase))
            {
                return "net8.0";
            }

            if (frameworkName.Contains("net9.0", StringComparison.OrdinalIgnoreCase))
            {
                return "net9.0";
            }

            if (frameworkName.Contains("net10.0", StringComparison.OrdinalIgnoreCase))
            {
                return "net10.0";
            }

            return "net10.0";
        }

        private static string[] GetDacpacToolCandidatePaths(string preferredTargetFramework)
        {
            var orderedTargetFrameworks = DacpacToolTargetFrameworks
                .Where(tfm => !string.Equals(tfm, preferredTargetFramework, StringComparison.OrdinalIgnoreCase))
                .Prepend(preferredTargetFramework)
                .ToArray();

            return BuildConfigurations
                .SelectMany(configuration => orderedTargetFrameworks.Select(targetFramework =>
                    Path.GetFullPath(
                        Path.Combine(
                            AppContext.BaseDirectory,
                            "../../../../../src/DacpacTool/bin",
                            configuration,
                            targetFramework,
                            "DacpacTool.dll"))))
                .ToArray();
        }

        private static string ComputeSha256(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash);
        }
    }
}
