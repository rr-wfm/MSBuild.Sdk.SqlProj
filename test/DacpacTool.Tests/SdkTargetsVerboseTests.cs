using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class SdkTargetsVerboseTests
    {
        [TestMethod]
        public void BuildIsQuietByDefault()
        {
            var result = RunBuild(verbose: false);

            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.ShouldNotContain("Using SQL Server version Sql150");
            result.Output.ShouldNotContain("Adding SqlCmd variable");
        }

        [TestMethod]
        public void BuildUsesVerboseLoggingWhenEnabled()
        {
            var result = RunBuild(verbose: true);

            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.ShouldContain("Using SQL Server version Sql150");
        }

        private static (int ExitCode, string Output) RunBuild(bool verbose)
        {
            var repoRoot = GetRepositoryRoot();
            var projectPath = Path.Combine(repoRoot, "test", "TestProject", "TestProject.csproj");
            var tempRoot = Path.Combine(Path.GetTempPath(), "MSBuildSdkSqlProjVerboseTests", Guid.NewGuid().ToString("N"));
            var outputPath = Path.Combine(tempRoot, "out");
            var intermediatePath = Path.Combine(tempRoot, "obj");

            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(intermediatePath);

            try
            {
                var prepareResult = RunDotNetCommand(
                    repoRoot,
                    Path.Combine(repoRoot, "src", "MSBuild.Sdk.SqlProj", "MSBuild.Sdk.SqlProj.csproj"));
                prepareResult.ExitCode.ShouldBe(0, prepareResult.Output);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = repoRoot,
                };

                startInfo.ArgumentList.Add("build");
                startInfo.ArgumentList.Add(projectPath);
                startInfo.ArgumentList.Add("/t:Rebuild");
                startInfo.ArgumentList.Add("/nologo");
                startInfo.ArgumentList.Add("/v:minimal");
                startInfo.ArgumentList.Add($"/p:BaseOutputPath={EnsureTrailingSeparator(outputPath)}");
                startInfo.ArgumentList.Add($"/p:BaseIntermediateOutputPath={EnsureTrailingSeparator(intermediatePath)}");

                if (verbose)
                {
                    startInfo.ArgumentList.Add("/p:MSBuildSdkSqlProjVerbose=True");
                }

                using var process = Process.Start(startInfo);
                process.ShouldNotBeNull();

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static (int ExitCode, string Output) RunDotNetCommand(string repoRoot, string projectPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot,
            };

            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("/nologo");
            startInfo.ArgumentList.Add("/v:minimal");

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, standardOutput + Environment.NewLine + standardError);
        }

        private static string GetRepositoryRoot()
        {
            var testProjectDirectory = ProjectSource.ProjectDirectory();
            return Path.GetFullPath(Path.Combine(testProjectDirectory, "..", ".."));
        }
    }
}
