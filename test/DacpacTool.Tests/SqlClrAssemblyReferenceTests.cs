using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.IO.Packaging;
using System.Text;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    /// <summary>
    /// End-to-end test that builds <c>TestProjectWithSqlClrReference</c> and verifies that the
    /// resulting dacpac contains both the SQL CLR function and the referenced CLR assembly.
    /// </summary>
    [TestClass]
    public class SqlClrAssemblyReferenceTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void Build_TestProjectWithSqlClrReference_ProducesDacpacWithAssemblyAndFunction()
        {
            // Arrange - locate the scenario project relative to the test assembly
            // (test/DacpacTool.Tests/bin/<config>/<tfm>/ -> ../../../../TestProjectWithSqlClrReference)
            var assemblyDir = Path.GetDirectoryName(typeof(SqlClrAssemblyReferenceTests).Assembly.Location)!;
            var projectPath = Path.GetFullPath(Path.Combine(
                assemblyDir, "..", "..", "..", "..",
                "TestProjectWithSqlClrReference", "TestProjectWithSqlClrReference.csproj"));
            File.Exists(projectPath).ShouldBeTrue($"Scenario project not found at '{projectPath}'.");

            var dacpacPath = Path.Combine(Path.GetDirectoryName(projectPath)!,
                "bin", "Release", "netstandard2.0", "TestProjectWithSqlClrReference.dacpac");

            // Ensure a clean build
            if (File.Exists(dacpacPath))
            {
                File.Delete(dacpacPath);
            }

            // Act - invoke dotnet build for the scenario project
            var psi = new ProcessStartInfo("dotnet",
                $"build \"{projectPath}\" -c Release --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            TestContext.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                TestContext.WriteLine("STDERR:");
                TestContext.WriteLine(stderr);
            }

            // Assert - build succeeded
            process.ExitCode.ShouldBe(0,
                $"Build of TestProjectWithSqlClrReference failed.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            File.Exists(dacpacPath).ShouldBeTrue($"Expected dacpac at '{dacpacPath}'.");

            // Assert - dacpac contains the CLR function and the assembly
            using var package = DacPackage.Load(dacpacPath, DacSchemaModelStorageType.Memory);
            using var model = TSqlModel.LoadFromDacpac(dacpacPath,
                new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: false));

            var function = model.GetObject(ScalarFunction.TypeClass,
                new ObjectIdentifier("dbo", "fn_ReturnOne"), DacQueryScopes.Default);
            function.ShouldNotBeNull("Expected scalar function [dbo].[fn_ReturnOne] in dacpac.");

            var assemblies = model.GetObjects(DacQueryScopes.Default, Assembly.TypeClass).ToList();
            assemblies.ShouldContain(a => a.Name.Parts.Contains("SqlClrTestLibrary"),
                "Expected CREATE ASSEMBLY [SqlClrTestLibrary] in dacpac.");

            // Assert - a procedure that references the deferred function is also present and its
            // dependency was correctly resolved (i.e. it was deferred transitively and re-added).
            var procedure = model.GetObject(Procedure.TypeClass,
                new ObjectIdentifier("dbo", "usp_SelectOne"), DacQueryScopes.Default);
            procedure.ShouldNotBeNull("Expected stored procedure [dbo].[usp_SelectOne] in dacpac.");
            var referenced = procedure.GetReferenced().Select(o => o.Name.ToString()).ToList();
            referenced.ShouldContain("[dbo].[fn_ReturnOne]",
                $"Procedure [dbo].[usp_SelectOne] should reference [dbo].[fn_ReturnOne], but only references: {string.Join(", ", referenced)}");
        }        

        [TestMethod]
        public void Build_TestProjectWithSqlClrReference_WithTrustInPreDeploy_InjectsTrustScript()
        {
            // Arrange - locate the dedicated scenario project that opts into trust injection.
            var assemblyDir = Path.GetDirectoryName(typeof(SqlClrAssemblyReferenceTests).Assembly.Location)!;
            var projectPath = Path.GetFullPath(Path.Combine(
                assemblyDir, "..", "..", "..", "..",
                "TestProjectWithSqlClrAssemblyTrust", "TestProjectWithSqlClrAssemblyTrust.csproj"));
            File.Exists(projectPath).ShouldBeTrue($"Scenario project not found at '{projectPath}'.");

            var dacpacPath = Path.Combine(Path.GetDirectoryName(projectPath)!,
                "bin", "Release", "netstandard2.0", "TestProjectWithSqlClrAssemblyTrust.dacpac");

            if (File.Exists(dacpacPath))
            {
                File.Delete(dacpacPath);
            }

            // Act - invoke dotnet build for the scenario project
            var psi = new ProcessStartInfo("dotnet",
                $"build \"{projectPath}\" -c Release --nologo --verbosity normal")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            TestContext.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                TestContext.WriteLine("STDERR:");
                TestContext.WriteLine(stderr);
            }

            // Assert - build succeeded
            process.ExitCode.ShouldBe(0,
                $"Build of TestProjectWithSqlClrAssemblyTrust failed.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            File.Exists(dacpacPath).ShouldBeTrue($"Expected dacpac at '{dacpacPath}'.");

            // Assert - the dacpac's predeploy.sql part contains the trust script for the assembly
            string predeploy;
            using (var package = Package.Open(dacpacPath, FileMode.Open, FileAccess.Read))
            {
                var partUri = new System.Uri("/predeploy.sql", System.UriKind.Relative);
                package.PartExists(partUri).ShouldBeTrue("Expected /predeploy.sql part in dacpac.");
                using var stream = package.GetPart(partUri).GetStream(FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                predeploy = reader.ReadToEnd();
            }

            predeploy.ShouldContain("BEGIN MSBuild.Sdk.SqlProj: trust referenced SQL CLR assemblies");
            predeploy.ShouldContain("sys.sp_add_trusted_assembly");
            // The hash is computed at build time and embedded as a 64-byte (128 hex char) literal,
            // so the predeploy script must NOT carry the assembly bytes nor a HASHBYTES call.
            predeploy.ShouldNotContain("HASHBYTES");
            predeploy.ShouldNotContain("varbinary(max)");
            predeploy.ShouldMatch(@"DECLARE @hash varbinary\(64\) = 0x[0-9A-F]{128};");
            predeploy.ShouldContain("'SqlClrTestLibrary'");
        }
    }
}
