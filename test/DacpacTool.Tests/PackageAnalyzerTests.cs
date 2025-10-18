using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests;

[TestClass]
public class PackageAnalyzerTests
{
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
        testConsole.Lines.ShouldNotContain("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the readme here: https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/blob/master/README.md#static-code-analysis. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
        testConsole.Lines.ShouldContain($"proc1.sql(1,47): Error SRD0006 : SqlServer.Rules : Avoid using SELECT *.");
        testConsole.Lines.ShouldContain($"-1(1,1): Error SRD0002 : SqlServer.Rules : Table does not have a primary key.");
        testConsole.Lines.Count(l => l.Contains("): Error ")).ShouldBe(2);
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

        testConsole.Lines[1].ShouldBe("DacpacTool warning SQLPROJ0001: No additional well-known rules files found, consider adding more rules via PackageReference - see the readme here: https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/blob/master/README.md#static-code-analysis. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0001</NoWarn>' to your project file.");
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
        packageBuilder.UsingVersion(SqlServerVersion.Sql150);
        packageBuilder.AddInputFile(new FileInfo("./Suppression/proc1.sql"));
        packageBuilder.AddInputFile(new FileInfo("./Suppression/proc2.sql"));
        packageBuilder.AddInputFile(new FileInfo("./Suppression/Folder/proc3.sql"));
        packageBuilder.SetMetadata("TestSuppression", "1.0.0");

        packageBuilder.ValidateModel();
        packageBuilder.SaveToDisk(path);

        var packageAnalyzer = new PackageAnalyzer(_console, null);

        try
        {
            //Set the current directory.
            Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(typeof(PackageAnalyzerTests).Assembly.Location) ?? string.Empty, "Suppression"));
            // Act
            packageAnalyzer.Analyze(packageBuilder.Model, path, Array.Empty<FileInfo>());
        }
        finally
        {
            //Reset the current directory.
            Directory.SetCurrentDirectory(Path.GetDirectoryName(typeof(PackageAnalyzerTests).Assembly.Location) ?? string.Empty);
        }

        // Assert
        testConsole.Lines.Count.ShouldBe(20);

        testConsole.Lines.Count(l => l.Contains("Warning SR0001 : Microsoft.Rules.Data")).ShouldBe(1);
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

    private FileInfo[] CollectAssemblyPaths()
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
    private static string CallerFilePath([CallerFilePath] string? callerFilePath = null) =>
        callerFilePath ?? throw new ArgumentNullException(nameof(callerFilePath));

    public static string ProjectDirectory() => Path.GetDirectoryName(CallerFilePath())!;
}
