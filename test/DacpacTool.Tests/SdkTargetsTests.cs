using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class SdkTargetsTests
    {
        [TestMethod]
        public void CopiesGeneratedCreateScriptToReferencingProjectOutput()
        {
            var repositoryRoot = FindRepositoryRoot();
            var sdkProjectPath = Path.Combine(repositoryRoot, "src", "MSBuild.Sdk.SqlProj", "MSBuild.Sdk.SqlProj.csproj");
            var referencingProjectPath = Path.Combine(repositoryRoot, "test", "TestIncludeFromVanillaProjWithGeneratedScript", "TestIncludeFromVanillaProjWithGeneratedScript.csproj");
            var referencingProjectDirectory = Path.GetDirectoryName(referencingProjectPath);
            var referencedProjectDirectory = Path.Combine(repositoryRoot, "test", "TestProjectWithGenerateScript");

            DeleteDirectoryIfExists(Path.Combine(referencingProjectDirectory, "bin"));
            DeleteDirectoryIfExists(Path.Combine(referencingProjectDirectory, "obj"));
            DeleteDirectoryIfExists(Path.Combine(referencedProjectDirectory, "bin"));
            DeleteDirectoryIfExists(Path.Combine(referencedProjectDirectory, "obj"));

            var sdkBuildResult = RunDotnetBuild("build \"" + sdkProjectPath + "\" -nologo", repositoryRoot);
            sdkBuildResult.ExitCode.ShouldBe(0, sdkBuildResult.Output);

            var buildResult = RunDotnetBuild("build \"" + referencingProjectPath + "\" -nologo", referencingProjectDirectory);
            buildResult.ExitCode.ShouldBe(0, buildResult.Output);

            var outputDirectory = Path.Combine(referencingProjectDirectory, "bin", "Debug", "net8.0");
            File.Exists(Path.Combine(outputDirectory, "TestProjectWithGenerateScript.dacpac")).ShouldBeTrue(buildResult.Output);
            File.Exists(Path.Combine(outputDirectory, "TestProjectWithGenerateScript_Create.sql")).ShouldBeTrue(buildResult.Output);
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root.");
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static (int ExitCode, string Output) RunDotnetBuild(string arguments, string workingDirectory)
        {
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (string.IsNullOrWhiteSpace(dotnetRoot))
            {
                throw new InvalidOperationException("DOTNET_ROOT is not set.");
            }

            var dotnetPath = Path.Combine(dotnetRoot, "dotnet");
            var startInfo = new ProcessStartInfo(dotnetPath, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            startInfo.Environment.Clear();
            CopyEnvironmentVariable("HOME");
            CopyEnvironmentVariable("USER");
            CopyEnvironmentVariable("LOGNAME");
            CopyEnvironmentVariable("TMPDIR");
            CopyEnvironmentVariable("TMP");
            CopyEnvironmentVariable("TEMP");
            CopyEnvironmentVariable("NUGET_PACKAGES");
            CopyEnvironmentVariable("XDG_CACHE_HOME");
            CopyEnvironmentVariable("XDG_DATA_HOME");
            CopyEnvironmentVariable("XDG_CONFIG_HOME");

            startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
            startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            startInfo.Environment["PATH"] = dotnetRoot + Path.PathSeparator + "/usr/bin" + Path.PathSeparator + "/bin";
            startInfo.Environment["LANG"] = "C.UTF-8";

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process {dotnetPath} {arguments}");
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, standardOutput + Environment.NewLine + standardError);

            void CopyEnvironmentVariable(string variableName)
            {
                var value = Environment.GetEnvironmentVariable(variableName);
                if (!string.IsNullOrEmpty(value))
                {
                    startInfo.Environment[variableName] = value;
                }
            }
        }
    }
}
