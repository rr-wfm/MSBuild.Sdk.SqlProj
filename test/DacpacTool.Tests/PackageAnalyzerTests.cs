using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PackageAnalyzerTests
    {
        private const string SuppressionFileName = global::Microsoft.SqlServer.Dac.CodeAnalysis.ProjectProblemSuppressor.SuppressionFilename;
        private const string SuppressionFileNameMixedCaseRuleIds = "StaticCodeAnalysis.SuppressMessages.mixed-rule-id-casing.xml";
        // IDE0330: keep object here because this test project also targets net8.0.
        private static readonly object SuppressionFileLock = new();
        private static readonly string[] AnalysisFailureErrors = ["proc1.sql(1,1): Error SR9999: Analyzer failed."];
        private static readonly string[] AnalysisFailureWarnings = ["proc1.sql(1,1): Warning SRD0006 : Should not be written."];
        private readonly IConsole _console = new TestConsole();

        [TestMethod]
        public void RunsAnalyzer()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, null);

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());
            
            // Assert
            testConsole.Lines.Count.ShouldBe(16);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Warning SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Warning SML005 : Smells : Avoid use of 'Select *'");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithSupressions()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD0006;-Smells.SML005;-SqlServer.Rules.SRD999;+!SqlServer.Rules.SRN0002;");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.Count.ShouldBe(14);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD0006")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("SML0005")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("Error SRN0002")).ShouldBeTrue();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWildcardSupressions()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.Count.ShouldBe(14);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD")).ShouldBeFalse();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWarningsAsErrors()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!SqlServer.Rules.SRD0006");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.Count.ShouldBe(16);

            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.Count(l => l.Contains("Error ")).ShouldBe(1);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWarningsAsErrors_ExactRuleIdsAreCaseInsensitive()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!sqlserver.rules.srd0006");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain("proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(1);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWarningsAsErrorsUsingWildcard()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!SqlServer.Rules.SRD*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.Count.ShouldBe(16);

            testConsole.Lines.Count(l => l.Contains("Using analyzers: ")).ShouldBe(1);
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldNotContain("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the documentation here: https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/static-code-analysis.html. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
            testConsole.Lines.ShouldContain($"proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain($"-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.");
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(2);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWildcardOverrides_CaseInsensitivePrefixes()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!sqlserver.rules.srd*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(2);
            testConsole.Lines.ShouldContain("proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain("-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.");
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithWildcardSuppressions_CaseInsensitivePrefixes()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-sqlserver.rules.srd*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD0006")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("SRD0002")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("Error")).ShouldBeFalse();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithDuplicateWildcardSuppressions_DifferentCasing_BehavesOnce()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "-SqlServer.Rules.SRD*;-sqlserver.rules.srd*");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Any(l => l.Contains("SRD0006")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("SRD0002")).ShouldBeFalse();
            testConsole.Lines.Any(l => l.Contains("): Error ")).ShouldBeFalse();
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        [DataRow("+!SqlServer.Rules.SRD0006", 1, "proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.",      true)]
        [DataRow("+!SqlServer.Rules.SRD*",    2, "proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.",      true)]
        [DataRow("+!SqlServer.Rules.SRD*",    2, "-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.", true)]
        [DataRow("-SqlServer.Rules.SRD0006",  0, "proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.",      false)]
        [DataRow("-SqlServer.Rules.SRD*",     0, "SRD0002",                                                                       false)]
        public void RunsAnalyzerWithRuleSeverityOverrides_DataDriven(string overrideRules, int expectedErrorCount, string expectedOutputLine, bool expectContainsLine)
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, overrideRules);

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            if (expectContainsLine)
            {
                testConsole.Lines.Any(l => l.Contains(expectedOutputLine)).ShouldBeTrue();
            }
            else
            {
                testConsole.Lines.Any(l => l.Contains(expectedOutputLine)).ShouldBeFalse();
            }

            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(expectedErrorCount);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzer_WithMultipleWarningToErrorOverridesInSingleExpression()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, "+!SqlServer.Rules.SRD0006;+!SqlServer.Rules.SRD0002;+!Smells.SML005");

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, CollectAssemblyPaths());

            // Assert
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.ShouldContain("proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
            testConsole.Lines.ShouldContain("-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.");
            testConsole.Lines.Any(l => l.Contains("): Error SML005 : Smells : Avoid use of 'Select *'")).ShouldBeTrue();
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(3);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithoutAdditionalAnalyzers()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var result = BuildSimpleModel();
            var packageAnalyzer = new PackageAnalyzer(_console, null);

            // Act
            packageAnalyzer.Analyze(result.model, result.fileInfo, Array.Empty<FileInfo>());

            // Assert
            testConsole.Lines.Count.ShouldBe(6);

            testConsole.Lines[1].ShouldBe("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the documentation here: https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/static-code-analysis.html. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
            testConsole.Lines.ShouldContain($"Analyzing package '{result.fileInfo.FullName}'");
            testConsole.Lines.Count(l => l.Contains("Using analyzers: ")).ShouldBe(1);
            testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(0);
            testConsole.Lines.ShouldContain($"Successfully analyzed package '{result.fileInfo.FullName}'");
        }

        [TestMethod]
        public void RunsAnalyzerWithSuppressionFile()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var path = new FileInfo(Path.GetTempFileName() + ".dacpac");

            var packageBuilder = new PackageBuilder(testConsole);
            packageBuilder.UsingVersion(SqlServerVersion.Sql160);
            packageBuilder.AddInputFile(new FileInfo("./Suppression/proc1.sql"));
            packageBuilder.AddInputFile(new FileInfo("./Suppression/proc2.sql"));
            packageBuilder.AddInputFile(new FileInfo("./Suppression/Folder/proc3.sql"));
            packageBuilder.SetMetadata("TestSuppression", "1.0.0");

            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(path);

            var packageAnalyzer = new PackageAnalyzer(_console, null);

            // PackageAnalyzer resolves the suppression file from CurrentDirectory,
            // so these tests must serialize any CurrentDirectory changes.
            lock (SuppressionFileLock)
            {
                try
                {
                    // Set the current directory.
                    Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(typeof(PackageAnalyzerTests).Assembly.Location), "Suppression"));
                    // Act
                    packageAnalyzer.Analyze(packageBuilder.Model, path, Array.Empty<FileInfo>());
                }
                finally
                {
                    // Reset the current directory.
                    Directory.SetCurrentDirectory(Path.GetDirectoryName(typeof(PackageAnalyzerTests).Assembly.Location));
                }
            }

            // Assert
            testConsole.Lines.Count.ShouldBe(20);

            testConsole.Lines.Count(l => l.Contains("Warning SR0001 : Microsoft.Rules.Data")).ShouldBe(1);
            testConsole.Lines.Any(l => l.Contains("Suppressing rule:")).ShouldBeTrue();
        }

        [TestMethod]
        public void WriteAnalysisResults_WhenAnalysisFails_WritesErrorsAndFailureMessage()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var packageAnalyzer = new PackageAnalyzer(_console, null);
            var method = typeof(PackageAnalyzer).GetMethod("WriteAnalysisResults", BindingFlags.NonPublic | BindingFlags.Instance);
            var outputFile = new FileInfo("test.dacpac");
            var serialized = false;

            // Act
            method!.Invoke(packageAnalyzer, new object[]
            {
                outputFile,
                AnalysisFailureErrors,
                false,
                AnalysisFailureWarnings,
                new Action(() => serialized = true)
            });

            // Assert
            testConsole.Lines.ShouldContain("proc1.sql(1,1): Error SR9999: Analyzer failed.");
            testConsole.Lines.ShouldContain($"Analysis of package '{outputFile}' failed");
            testConsole.Lines.ShouldNotContain("proc1.sql(1,1): Warning SRD0006 : Should not be written.");
            testConsole.Lines.ShouldNotContain($"Successfully analyzed package '{outputFile}'");
            serialized.ShouldBeFalse();
        }

        [TestMethod]
        public void RunsAnalyzerWithSuppressionFile_RuleIdMatchingIsCaseSensitive()
        {
            // Arrange
            // The mixed-case fixture suppresses proc1.sql with SR0001 and tries to suppress Folder/proc3.sql with sr0001.
            // That lets the test prove the matching remains case-sensitive while exercising the same directory layout as the existing suppression test.
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();

            var path = new FileInfo(Path.GetTempFileName() + ".dacpac");

            var packageBuilder = new PackageBuilder(testConsole);
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.AddInputFile(new FileInfo("./Suppression/proc1.sql"));
            packageBuilder.AddInputFile(new FileInfo("./Suppression/proc2.sql"));
            packageBuilder.AddInputFile(new FileInfo("./Suppression/Folder/proc3.sql"));
            packageBuilder.SetMetadata("TestSuppression", "1.0.0");

            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(path);

            var originalDirectory = Environment.CurrentDirectory;
            var packageAnalyzer = new PackageAnalyzer(_console, null);
            var suppressionDirectory = Path.Combine(
                Path.GetDirectoryName(typeof(PackageAnalyzerTests).Assembly.Location)!,
                "Suppression",
                string.Empty);
            var originalSuppressionFile = Path.Combine(suppressionDirectory, SuppressionFileName);
            var mixedCaseSuppressionFile = Path.Combine(suppressionDirectory, SuppressionFileNameMixedCaseRuleIds);
            var backupSuppressionFile = originalSuppressionFile + ".bak";

            // PackageAnalyzer resolves the suppression file from CurrentDirectory,
            // and this test swaps the shared suppression fixture in place.
            lock (SuppressionFileLock)
            {
                try
                {
                    File.Move(originalSuppressionFile, backupSuppressionFile, overwrite: true);
                    File.Copy(mixedCaseSuppressionFile, originalSuppressionFile);
                    Directory.SetCurrentDirectory(suppressionDirectory);
                    testConsole.Lines.Clear();
                    packageAnalyzer.Analyze(packageBuilder.Model, path, Array.Empty<FileInfo>());

                    // proc1.sql is suppressed by the correctly cased RuleId.
                    testConsole.Lines.Any(l => l.Contains("proc1.sql") && l.Contains("Warning SR0001 : Microsoft.Rules.Data")).ShouldBeFalse();
                    // proc3.sql is not suppressed because the fixture uses sr0001 instead of SR0001!!
                    testConsole.Lines.Any(l => l.Contains("proc3.sql") && l.Contains("Warning SR0001 : Microsoft.Rules.Data")).ShouldBeTrue();
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDirectory);
                    if (File.Exists(backupSuppressionFile))
                    {
                        if (File.Exists(originalSuppressionFile))
                        {
                            File.Delete(originalSuppressionFile);
                        }

                        File.Move(backupSuppressionFile, originalSuppressionFile);
                    }
                }
            }
        }

        private static (FileInfo fileInfo, TSqlModel model) BuildSimpleModel()
        {
            var tmodel = new TestModelBuilder()
                .AddTable("TestTable", ("Column1", "nvarchar(100)"))
                .AddStoredProcedure("sp_GetData", "SELECT * FROM dbo.TestTable", "proc1.sql");

            var model = tmodel.Build();
            var packagePath = tmodel.SaveAsPackage();

            return (new FileInfo(packagePath), model);
        }

        private static FileInfo[] CollectAssemblyPaths()
        {
            var result = new List<FileInfo>();
            var path = ProjectSource.ProjectDirectory();
            result.Add(new FileInfo(Path.Combine(path, "SqlServer.Rules.dll")));
            result.Add(new FileInfo(Path.Combine(path, "TSQLSmellSCA.dll")));

            return result.ToArray();
        }
    }

    internal static class ProjectSource
    {
        private static string CallerFilePath([CallerFilePath] string callerFilePath = null) =>
            callerFilePath ?? throw new ArgumentNullException(nameof(callerFilePath));

        public static string ProjectDirectory() => Path.GetDirectoryName(CallerFilePath())!;
    }
}
