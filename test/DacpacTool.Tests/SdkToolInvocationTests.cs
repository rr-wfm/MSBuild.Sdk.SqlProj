using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class SdkToolInvocationTests
    {
        private static readonly XNamespace Msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
        private static readonly string SdkProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MSBuild.Sdk.SqlProj"));
        private DirectoryInfo directory;
        private string toolPath;
        private string projectPath;
        private string propertiesPath;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            directory = Directory.CreateTempSubdirectory("SqlProjTool ");
            var sdkDirectory = Path.Combine(directory.FullName, "Sdk");
            Directory.CreateDirectory(sdkDirectory);
            toolPath = Path.Combine(directory.FullName, "tools", "DacpacTool", "DacpacTool.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(toolPath));
            File.WriteAllText(toolPath, string.Empty);
            propertiesPath = Path.Combine(directory.FullName, "properties.txt");
            projectPath = Path.Combine(directory.FullName, "Inspect.proj");

            // Exercise the production target without unrelated SDK build prerequisites.
            new XDocument(new XElement(Msbuild + "Project", LoadSdkTarget("ValidateEnvironment")))
                .Save(Path.Combine(sdkDirectory, "Sdk.targets"));
            new XDocument(new XElement(Msbuild + "Project",
                new XElement(Msbuild + "Import", new XAttribute("Project", Path.Combine(sdkDirectory, "Sdk.targets"))),
                new XElement(Msbuild + "Target", new XAttribute("Name", "InspectEnvironment"), new XAttribute("DependsOnTargets", "ValidateEnvironment"),
                    new XElement(Msbuild + "WriteLinesToFile",
                        new XAttribute("File", propertiesPath),
                        new XAttribute("Lines", "$(DacpacToolExe)"),
                        new XAttribute("Overwrite", "true")))))
                .Save(projectPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            directory.Delete(true);
        }

        [TestMethod]
        [DataRow("8.0")]
        [DataRow("9.0")]
        [DataRow("10.0")]
        public async Task ValidateEnvironment_UsesSingleToolForSupportedSdk(string bundledFramework)
        {
            var result = await InspectEnvironment($"-p:BundledNETCoreAppTargetFrameworkVersion={bundledFramework}");

            result.ExitCode.ShouldBe(0, result.Output);
            Path.GetFullPath(File.ReadAllLines(propertiesPath).Single()).ShouldBe(toolPath);
        }

        [TestMethod]
        public async Task ValidateEnvironment_PreservesExplicitToolPath()
        {
            var customTool = Path.Combine(directory.FullName, "custom tool", "DacpacTool.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(customTool));
            File.WriteAllText(customTool, string.Empty);

            var result = await InspectEnvironment($"-p:DacpacToolExe={customTool}");

            result.ExitCode.ShouldBe(0, result.Output);
            File.ReadAllLines(propertiesPath).ShouldBe(new[] { customTool });
        }

        [TestMethod]
        public async Task ValidateEnvironment_ReportsMissingTool()
        {
            var missingTool = Path.Combine(directory.FullName, "missing tool", "DacpacTool.dll");

            var result = await InspectEnvironment($"-p:DacpacToolExe={missingTool}");

            result.ExitCode.ShouldNotBe(0);
            result.Output.ShouldContain($"DacpacTool was not found at '{missingTool}'");
            result.Output.ShouldContain("Restore or reinstall the MSBuild.Sdk.SqlProj package");
            result.Output.ShouldNotContain("which is not supported");
            File.Exists(propertiesPath).ShouldBeFalse();
        }

        [TestMethod]
        [DataRow("GetIncludedFiles", "collect-includes")]
        [DataRow("CoreCompile", "build")]
        [DataRow("PublishDatabase", "deploy")]
        public void SdkCommand_UsesToolRuntimeConfiguration(string targetName, string command)
        {
            var target = LoadSdkTarget(targetName);

            var toolCommand = target.Descendants(Msbuild + "DacpacToolCommand").Single().Value;

            toolCommand.ShouldStartWith($"dotnet \"$(DacpacToolExe)\" {command} ");
            toolCommand.ShouldNotContain("--roll-forward");
        }

        [TestMethod]
        public void DacpacToolRuntimeConfiguration_UsesMajorRollForward()
        {
            using var runtimeConfig = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(DacpacToolPath.FilePath, "runtimeconfig.json")));

            var runtimeOptions = runtimeConfig.RootElement.GetProperty("runtimeOptions");

            runtimeOptions.GetProperty("tfm").GetString().ShouldBe("net8.0");
            runtimeOptions.GetProperty("rollForward").GetString().ShouldBe("Major");
            runtimeOptions.TryGetProperty("rollForwardOnNoCandidateFx", out _).ShouldBeFalse();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SdkProject_RestoresBuildsAndPublishesBuildOnlyTool(bool specifyFramework)
        {
            var toolDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "src", "DacpacTool"));
            var sdkDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "src", "MSBuild.Sdk.SqlProj"));
            var sdkProjectPath = Path.Combine(sdkDirectory.FullName, "MSBuild.Sdk.SqlProj.msbuildproj");
            var sdkProject = XDocument.Load(Path.Combine(SdkProjectDirectory, "MSBuild.Sdk.SqlProj.msbuildproj"));
            sdkProject.Save(sdkProjectPath);
            var toolProject = XDocument.Load(Path.Combine(SdkProjectDirectory, "..", "DacpacTool", "DacpacTool.csproj"));
            var rollForward = toolProject.Root.Elements("PropertyGroup").Elements("RollForward").Single();
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("OutputType", "Exe"),
                    new XElement("TargetFramework", "net8.0"),
                    new XElement(rollForward)),
                new XElement("ItemGroup",
                    new XElement("Content", new XAttribute("Include", "tool-content.txt"),
                        new XAttribute("CopyToOutputDirectory", "PreserveNewest")))))
                .Save(Path.Combine(toolDirectory.FullName, "DacpacTool.csproj"));
            File.WriteAllText(Path.Combine(toolDirectory.FullName, "Program.cs"), "System.Console.WriteLine(\"Publish fixture\");");
            File.WriteAllText(Path.Combine(toolDirectory.FullName, "tool-content.txt"), "Published tool content");

            var arguments = new[] { "build", sdkProjectPath, "-nologo", "-c", "Release" };
            if (specifyFramework)
            {
                arguments = [.. arguments, "-p:TargetFramework=netstandard2.0"];
            }

            var result = await RunDotnet(arguments);

            result.ExitCode.ShouldBe(0, result.Output);
            File.Exists(Path.Combine(toolDirectory.FullName, "obj", "project.assets.json")).ShouldBeTrue();
            var builtTool = Path.Combine(toolDirectory.FullName, "bin", "Release", "net8.0", "DacpacTool.dll");
            File.Exists(builtTool).ShouldBeTrue();
            var publishDirectory = Path.Combine(sdkDirectory.FullName, "tools", "DacpacTool");
            File.ReadAllBytes(Path.Combine(publishDirectory, "DacpacTool.dll")).ShouldBe(File.ReadAllBytes(builtTool));
            File.ReadAllText(Path.Combine(publishDirectory, "tool-content.txt")).ShouldBe("Published tool content");
            using var runtimeConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(publishDirectory, "DacpacTool.runtimeconfig.json")));
            runtimeConfig.RootElement.GetProperty("runtimeOptions").GetProperty("tfm").GetString().ShouldBe("net8.0");
            runtimeConfig.RootElement.GetProperty("runtimeOptions").GetProperty("rollForward").GetString().ShouldBe("Major");
            var sdkOutputFiles = Directory.GetFiles(Path.Combine(sdkDirectory.FullName, "bin"), "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName).ToArray();
            sdkOutputFiles.ShouldNotContain("DacpacTool.dll");
            sdkOutputFiles.ShouldNotContain("tool-content.txt");
            var reference = sdkProject.Root.Elements("ItemGroup").Elements("ProjectReference").Single();
            ((string)reference.Attribute("ReferenceOutputAssembly")).ShouldBe("false");
            ((string)reference.Attribute("Private")).ShouldBe("false");
            ((string)reference.Attribute("PrivateAssets")).ShouldBe("all");
        }

        [TestMethod]
        public void SdkPackage_UsesDedicatedToolDirectory()
        {
            var sdkProject = XDocument.Load(Path.Combine(SdkProjectDirectory, "MSBuild.Sdk.SqlProj.msbuildproj"));
            var collectFiles = sdkProject.Root.Elements("Target").Single(target => (string)target.Attribute("Name") == "CollectPackageFiles");

            var toolFiles = collectFiles.Elements("ItemGroup").Elements("None")
                .Where(file => ((string)file.Attribute("Include")).StartsWith("tools", StringComparison.Ordinal)).ToArray();

            ((string)collectFiles.Attribute("BeforeTargets")).ShouldBe("_GetPackageFiles");
            toolFiles.Length.ShouldBe(1);
            ((string)toolFiles[0].Attribute("Include")).ShouldBe(@"tools\DacpacTool\**\*");
            ((string)toolFiles[0].Attribute("Pack")).ShouldBe("true");
            ((string)toolFiles[0].Attribute("PackagePath")).ShouldBe(@"tools\DacpacTool\%(RecursiveDir)");
        }

        private static XElement LoadSdkTarget(string name)
        {
            var targetsPath = Path.Combine(SdkProjectDirectory, "Sdk", "Sdk.targets");
            var targets = XDocument.Load(targetsPath);
            return new XElement(targets.Root.Elements(Msbuild + "Target").Single(target => (string)target.Attribute("Name") == name));
        }

        private Task<(int ExitCode, string Output)> InspectEnvironment(params string[] properties)
        {
            return RunDotnet(["msbuild", projectPath, "-nologo", "-t:InspectEnvironment", .. properties]);
        }

        private async Task<(int ExitCode, string Output)> RunDotnet(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            startInfo.ArgumentList.Add($"-bl:{Path.Combine(directory.FullName, $"{Guid.NewGuid():N}.binlog")}");
            foreach (var name in startInfo.Environment.Keys.Where(name =>
                name.StartsWith("CustomBeforeMicrosoftCommon", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("CustomAfterMicrosoftCommon", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                startInfo.Environment.Remove(name);
            }

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
            await process.WaitForExitAsync(TestContext.CancellationToken);
            return (process.ExitCode, await stdout + await stderr);
        }
    }
}
