using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
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
            startInfo.ArgumentList.Add($"-p:DacpacToolExe={DacpacToolPath.FilePath}");
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
            var propertiesOutput = EvaluateProject(useArtifactsOutput, "-getProperty:OutputPath,IntermediateOutputPath");
            var (outputPath, intermediateOutputPath) = GetOutputDirectories(propertiesOutput);
            var fixtureFiles = new[]
            {
                Path.Combine(outputPath, "TestProjectWithPrePost.dacpac"),
                Path.Combine(outputPath, "Generated.sql"),
                Path.Combine(intermediateOutputPath, "Generated.txt"),
                Path.Combine(intermediateOutputPath, "Generated.sql"),
            };

            try
            {
                Directory.CreateDirectory(outputPath);
                Directory.CreateDirectory(intermediateOutputPath);
                foreach (var fixtureFile in fixtureFiles)
                {
                    File.WriteAllText(fixtureFile, string.Empty);
                }

                var itemsOutput = EvaluateProject(useArtifactsOutput,
                    "-getItem:None;Content;Folder;UpToDateCheckInput",
                    "-p:BuildingInsideVisualStudio=true",
                    "-p:DesignTimeBuild=true");
                AssertItemsExcludeDirectories(itemsOutput, outputPath, intermediateOutputPath);
            }
            finally
            {
                foreach (var fixtureFile in fixtureFiles)
                {
                    File.Delete(fixtureFile);
                }
            }
        }

        [TestMethod]
        [DataRow("PrePlan", true, true, DisplayName = "Pre-plan nested includes")]
        [DataRow("PreDeploy", true, true, DisplayName = "Pre-deployment nested includes")]
        [DataRow("PostDeploy", true, true, DisplayName = "Post-deployment nested includes")]
        [DataRow("PrePlan", false, true, DisplayName = "Outside Visual Studio")]
        [DataRow("PrePlan", true, false, DisplayName = "Outside design-time builds")]
        [DataRow("", true, true, DisplayName = "No deployment scripts")]
        public void CollectUpToDateCheckInputsDesignTime_TracksIncludesOnlyInDesignTimeBuilds(
            string itemType, bool buildingInsideVisualStudio, bool designTimeBuild)
        {
            var fixtureDirectory = Directory.CreateTempSubdirectory("SqlProjUpToDateCheck-");

            try
            {

                // Arrange

                var (entryScript, firstInclude, nestedInclude, overridesPath) = CreateIncludeFixture(fixtureDirectory, itemType);
                var expectIncludes = buildingInsideVisualStudio && designTimeBuild && itemType.Length > 0;
                var toolPath = expectIncludes
                    ? DacpacToolPath.FilePath
                    : Path.Combine(fixtureDirectory.FullName, "MissingTool.dll");
                var expectedPaths = new List<string>
                {
                    Path.Combine(ProjectDirectory, "Tables", "MyTable.sql"),
                    Path.Combine(ProjectDirectory, "RefactorLog", "TestProjectWithPrePost.refactorlog"),
                };

                if (itemType.Length > 0)
                {
                    expectedPaths.Add(entryScript);
                }

                if (expectIncludes)
                {
                    expectedPaths.Add(firstInclude);
                    expectedPaths.Add(nestedInclude);
                }

                // Act

                var output = EvaluateProject(false, "-getItem:UpToDateCheckInput",
                    "-target:CollectUpToDateCheckInputDesignTime",
                    $"-p:CustomBeforeNoTargets={overridesPath}",
                    $"-p:BuildingInsideVisualStudio={buildingInsideVisualStudio}",
                    $"-p:DesignTimeBuild={designTimeBuild}",
                    "-p:BuildingProject=false",
                    $"-p:DacpacToolExe={toolPath}",
                    "-verbosity:quiet");

                using var document = JsonDocument.Parse(output);
                var paths = document.RootElement.GetProperty("Items")
                    .GetProperty("UpToDateCheckInput")
                    .EnumerateArray()
                    .Select(item => item.GetProperty("FullPath").GetString()
                        ?? throw new InvalidDataException("An up-to-date-check input has no path."))
                    .ToArray();

                // Assert

                paths.ShouldBe(expectedPaths, ignoreOrder: true);
            }
            finally
            {
                fixtureDirectory.Delete(recursive: true);
            }
        }

        [TestMethod]
        [DataRow(true, true, DisplayName = "Design-time build")]
        [DataRow(false, false, DisplayName = "Not building the project")]
        public void GetIncludedFiles_OutsideNormalBuild_SkipsDiscovery(bool designTimeBuild, bool buildingProject)
        {

            // Arrange

            var missingToolPath = Path.Combine(ProjectDirectory, $"MissingTool-{Guid.NewGuid():N}.dll");

            // Act

            var output = EvaluateProject(false, "-getProperty:IncludedFiles",
                "-target:GetIncludedFiles",
                $"-p:DesignTimeBuild={designTimeBuild}",
                $"-p:BuildingProject={buildingProject}",
                $"-p:DacpacToolExe={missingToolPath}",
                "-verbosity:quiet");

            // Assert

            output.Trim().ShouldBeEmpty();
        }

        [TestMethod]
        public void BuildProject_IncludedScriptChanges_UpdatesArtifactsAndSkipsUnchangedBuild()
        {
            var fixtureDirectory = Directory.CreateTempSubdirectory("SqlProjIncrementalBuild-");

            try
            {

                // Arrange

                var (entryScript, firstInclude, nestedInclude, overridesPath) = CreateIncludeFixture(fixtureDirectory, "PostDeploy");
                var entryTimestamp = File.GetLastWriteTimeUtc(entryScript);
                var firstIncludeTimestamp = File.GetLastWriteTimeUtc(firstInclude);
                var artifactsPath = Path.Combine(fixtureDirectory.FullName, "artifacts");

                string BuildProject() => EvaluateProject(false, "-getProperty:TargetPath,IncludedFiles",
                    "-restore",
                    "-target:Build",
                    $"-p:CustomBeforeNoTargets={overridesPath}",
                    $"-p:DacpacToolExe={DacpacToolPath.FilePath}",
                    "-p:UseArtifactsOutput=true",
                    $"-p:ArtifactsPath={artifactsPath}",
                    "-p:GenerateCreateScript=true",
                    "-p:BuildingInsideVisualStudio=false",
                    "-p:DesignTimeBuild=false",
                    "-p:BuildingProject=true",
                    "-verbosity:quiet");

                // Act

                var firstBuildOutput = BuildProject();
                using var document = JsonDocument.Parse(firstBuildOutput);
                var dacpacFile = GetFullProjectPath(document.RootElement.GetProperty("Properties"), "TargetPath");
                var createScriptFile = Path.Combine(Path.GetDirectoryName(dacpacFile), "TestProjectWithPrePost_Create.sql");

                // Assert

                File.Exists(dacpacFile).ShouldBeTrue();
                File.ReadAllText(createScriptFile).ShouldContain("PRINT N'Nested include';");

                // Act

                const string updatedScript = "PRINT N'Updated nested include';";
                File.WriteAllText(nestedInclude, updatedScript);
                File.SetLastWriteTimeUtc(nestedInclude, File.GetLastWriteTimeUtc(dacpacFile).AddTicks(1));
                BuildProject();

                // Assert

                File.GetLastWriteTimeUtc(entryScript).ShouldBe(entryTimestamp);
                File.GetLastWriteTimeUtc(firstInclude).ShouldBe(firstIncludeTimestamp);
                using (var package = DacPackage.Load(dacpacFile))
                {
                    using var postDeploymentStream = package.PostDeploymentScript;
                    postDeploymentStream.ShouldNotBeNull();
                    using var reader = new StreamReader(postDeploymentStream);
                    reader.ReadToEnd().ShouldContain(updatedScript);
                }

                var createScript = File.ReadAllText(createScriptFile);
                createScript.ShouldContain(updatedScript);
                createScript.ShouldNotContain("PRINT N'Nested include';");
                var dacpacTimestamp = File.GetLastWriteTimeUtc(dacpacFile);
                var createScriptTimestamp = File.GetLastWriteTimeUtc(createScriptFile);

                // Act

                BuildProject();

                // Assert

                File.GetLastWriteTimeUtc(dacpacFile).ShouldBe(dacpacTimestamp);
                File.GetLastWriteTimeUtc(createScriptFile).ShouldBe(createScriptTimestamp);
            }
            finally
            {
                fixtureDirectory.Delete(recursive: true);
            }
        }

        private static (string EntryScript, string FirstInclude, string NestedInclude, string OverridesPath) CreateIncludeFixture(
            DirectoryInfo fixtureDirectory, string itemType)
        {
            var entryScript = Path.Combine(fixtureDirectory.FullName, "Entry.sql");
            var firstInclude = Path.Combine(fixtureDirectory.FullName, "First include.sql");
            var nestedDirectory = Directory.CreateDirectory(
                Path.Combine(fixtureDirectory.FullName, "Nested scripts"));
            var nestedInclude = Path.Combine(nestedDirectory.FullName, "Second include.sql");
            var nestedRelativePath = Path.Combine("Nested scripts", "Second include.sql");

            File.WriteAllText(entryScript,
                $":r \"First include.sql\"{Environment.NewLine}");
            File.WriteAllText(firstInclude,
                $":r \"{nestedRelativePath}\"{Environment.NewLine}");
            File.WriteAllText(nestedInclude, "PRINT N'Nested include';");

            var scriptItems = new XElement("ItemGroup",
                new XElement("PrePlan", new XAttribute("Remove", "@(PrePlan)")),
                new XElement("PreDeploy", new XAttribute("Remove", "@(PreDeploy)")),
                new XElement("PostDeploy", new XAttribute("Remove", "@(PostDeploy)")));

            if (itemType.Length > 0)
            {
                scriptItems.Add(new XElement(itemType,
                    new XAttribute("Include", entryScript)));
            }

            var overridesPath = Path.Combine(fixtureDirectory.FullName, "Overrides.targets");

            // Supply the Visual Studio collection contract for CLI MSBuild.
            new XElement("Project",
                scriptItems,
                new XElement("Target",
                    new XAttribute("Name", "CollectUpToDateCheckInputDesignTime"),
                    new XAttribute("Returns", "@(UpToDateCheckInput)")),
                new XElement("Target",
                    new XAttribute("Name", "FailIfCompilationRuns"),
                    new XAttribute("BeforeTargets", "CoreCompile"),
                    new XAttribute("Condition", "'$(DesignTimeBuild)' == 'true'"),
                    new XElement("Error",
                        new XAttribute("Text", "Design-time input collection must not compile the project."))))
                .Save(overridesPath);

            return (entryScript, firstInclude, nestedInclude, overridesPath);
        }

        private static string EvaluateProject(bool useArtifactsOutput, string query, params string[] additionalArguments)
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
            startInfo.ArgumentList.Add(query);
            startInfo.ArgumentList.Add("-p:Configuration=Release");
            startInfo.ArgumentList.Add("-nologo");

            foreach (var argument in additionalArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

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
            return output;
        }

        private static (string OutputPath, string IntermediateOutputPath) GetOutputDirectories(string msbuildOutput)
        {
            using var document = JsonDocument.Parse(msbuildOutput);
            var properties = document.RootElement.GetProperty("Properties");

            return (
                GetFullProjectPath(properties, "OutputPath"),
                GetFullProjectPath(properties, "IntermediateOutputPath"));
        }

        private static string GetFullProjectPath(JsonElement properties, string propertyName)
        {
            var path = properties.GetProperty(propertyName).GetString()
                ?? throw new InvalidDataException($"MSBuild property '{propertyName}' did not contain a path.");

            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, ProjectDirectory));
        }

        private static void AssertItemsExcludeDirectories(string msbuildOutput, params string[] outputDirectories)
        {
            using var document = JsonDocument.Parse(msbuildOutput);
            var items = document.RootElement.GetProperty("Items");

            foreach (var itemType in new[] { "None", "Content", "Folder", "UpToDateCheckInput" })
            {
                if (!items.TryGetProperty(itemType, out var itemGroup))
                {
                    continue;
                }

                foreach (var item in itemGroup.EnumerateArray())
                {
                    var fullPath = item.GetProperty("FullPath").GetString()
                        ?? throw new InvalidDataException($"MSBuild item '{itemType}' did not contain a full path.");
                    var isUnderOutputDirectory = outputDirectories.Any(directory =>
                        fullPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                    isUnderOutputDirectory.ShouldBeFalse($"Item '{fullPath}' should be excluded from evaluated project items.");
                }
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
