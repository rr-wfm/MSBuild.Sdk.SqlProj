using System;
using System.Diagnostics;
using System.IO;
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
        private static readonly string DacpacFile = Path.Combine(ProjectDirectory, "bin", "Release", "netstandard2.0", "TestProjectWithPrePost.dacpac");

        [TestMethod]
        public void BuildProject_EmbedsExpandedPrePlanScript()
        {
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

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var buildOutput = standardOutput + standardError;
            process.ExitCode.ShouldBe(0, buildOutput);
            File.Exists(DacpacFile).ShouldBeTrue(buildOutput);

            using var package = DacPackage.Load(DacpacFile);
            using var prePlanStream = package.PrePlanScript;
            prePlanStream.ShouldNotBeNull();
            using var reader = new StreamReader(prePlanStream);
            var prePlanScript = reader.ReadToEnd();

            prePlanScript.ShouldContain("PRINT N'Pre plan'");
            prePlanScript.ShouldContain("PRINT N'Pre-plan include'");
            prePlanScript.ShouldNotContain(":r Script1.sql");
        }
    }
}
