using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.SqlServer.Dac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class PrePlanProjectHarnessTests
    {
        private static readonly string ProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../TestProjectWithPrePost"));
        private static readonly string ProjectFile = Path.Combine(ProjectDirectory, "TestProjectWithPrePost.csproj");

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BuildProject_EmbedsExpandedPrePlanScript(bool useArtifactsOutput)
        {
            var dacpacFile = GetDacpacFile(useArtifactsOutput);
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = ProjectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(ProjectFile);
            startInfo.ArgumentList.Add("-nologo");
            startInfo.ArgumentList.Add("-t:Rebuild");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("Release");
            startInfo.ArgumentList.Add("-v:minimal");
            startInfo.ArgumentList.Add($"-p:DacpacToolExe={typeof(Program).Assembly.Location}");
            if (useArtifactsOutput)
            {
                startInfo.ArgumentList.Add("-p:UseArtifactsOutput=true");
                startInfo.ArgumentList.Add($"-p:ArtifactsPath={GetArtifactsPath()}");
            }

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var buildOutput = standardOutput + standardError;
            process.ExitCode.ShouldBe(0, buildOutput);
            File.Exists(dacpacFile).ShouldBeTrue(buildOutput);

            using var package = DacPackage.Load(dacpacFile);
            using var prePlanStream = package.PrePlanScript;
            prePlanStream.ShouldNotBeNull();
            using var reader = new StreamReader(prePlanStream);
            var prePlanScript = reader.ReadToEnd();

            prePlanScript.ShouldContain("PRINT N'Pre plan'");
            prePlanScript.ShouldContain("PRINT N'Pre-plan include'");
            prePlanScript.ShouldNotContain(":r Script1.sql");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void EvaluatedItemsExcludeOutputDirectories(bool useArtifactsOutput)
        {
            var outputPath = useArtifactsOutput
                ? Path.Combine(GetArtifactsPath(), "bin", "TestProjectWithPrePost", "release")
                : Path.Combine(ProjectDirectory, "bin", "Release", "netstandard2.0");
            var intermediateOutputPath = useArtifactsOutput
                ? Path.Combine(GetArtifactsPath(), "obj", "TestProjectWithPrePost", "release")
                : Path.Combine(ProjectDirectory, "obj", "Release", "netstandard2.0");

            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(intermediateOutputPath);
            File.WriteAllText(Path.Combine(outputPath, "TestProjectWithPrePost.dacpac"), string.Empty);
            File.WriteAllText(Path.Combine(outputPath, "Generated.sql"), string.Empty);
            File.WriteAllText(Path.Combine(intermediateOutputPath, "Generated.txt"), string.Empty);
            File.WriteAllText(Path.Combine(intermediateOutputPath, "Generated.sql"), string.Empty);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    WorkingDirectory = ProjectDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                startInfo.ArgumentList.Add("msbuild");
                startInfo.ArgumentList.Add(ProjectFile);
                startInfo.ArgumentList.Add("-getItem:None;Content;Folder");
                startInfo.ArgumentList.Add("-getProperty:OutputPath,IntermediateOutputPath");
                startInfo.ArgumentList.Add("-p:Configuration=Release");
                startInfo.ArgumentList.Add("-nologo");
                if (useArtifactsOutput)
                {
                    startInfo.ArgumentList.Add("-p:UseArtifactsOutput=true");
                    startInfo.ArgumentList.Add($"-p:ArtifactsPath={GetArtifactsPath()}");
                }

                using var process = Process.Start(startInfo);
                process.ShouldNotBeNull();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                process.ExitCode.ShouldBe(0, error + output);
                AssertItemsExcludeDirectories(output, outputPath, intermediateOutputPath);
            }
            finally
            {
                DeleteDirectory(GetArtifactsPath());
                DeleteDirectory(Path.Combine(ProjectDirectory, "bin"));
                DeleteDirectory(Path.Combine(ProjectDirectory, "obj"));
            }
        }

        private static void AssertItemsExcludeDirectories(string msbuildOutput, params string[] outputDirectories)
        {
            using var document = JsonDocument.Parse(msbuildOutput);
            var items = document.RootElement.GetProperty("Items");

            foreach (var itemType in new[] { "None", "Content", "Folder" })
            {
                if (!items.TryGetProperty(itemType, out var itemGroup))
                {
                    continue;
                }

                foreach (var item in itemGroup.EnumerateArray())
                {
                    var fullPath = item.GetProperty("FullPath").GetString();
                    var isUnderOutputDirectory = outputDirectories.Any(directory =>
                        fullPath.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                    isUnderOutputDirectory.ShouldBeFalse($"Item '{fullPath}' should be excluded from evaluated project items.");
                }
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static string GetDacpacFile(bool useArtifactsOutput)
        {
            return useArtifactsOutput
                ? Path.Combine(GetArtifactsPath(), "bin", "TestProjectWithPrePost", "release", "TestProjectWithPrePost.dacpac")
                : Path.Combine(ProjectDirectory, "bin", "Release", "netstandard2.0", "TestProjectWithPrePost.dacpac");
        }

        private static string GetArtifactsPath()
        {
            return Path.Combine(ProjectDirectory, "artifacts-test");
        }
    }
}
