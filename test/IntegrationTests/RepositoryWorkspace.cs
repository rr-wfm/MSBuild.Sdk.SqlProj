using System.Reflection;
using System.Xml.Linq;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

/// <summary>
/// Shared helpers for locating the repository checkout and preparing a temporary copy of
/// <c>test/TestProject</c> that builds against the SDK and DacpacTool from the checkout.
/// </summary>
internal static class RepositoryWorkspace
{
    private static readonly Lazy<string> LazyRoot = new(FindRoot);

    public static string Root => LazyRoot.Value;

    /// <summary>
    /// MSBuild property pointing builds at the DacpacTool built in this checkout.
    /// </summary>
    public static string DacpacToolProperty { get; } = BuildToolProperty();

    /// <summary>
    /// Copies <c>test/TestProject</c> into <paramref name="directory"/>, rewiring its SDK imports to
    /// the checkout and disabling the entity relationship diagram. Optionally overrides the
    /// SQL Server target platform. Returns the path to the written project file.
    /// </summary>
    public static string PrepareTestProject(DirectoryInfo directory, string? sqlServerVersion = null)
    {
        var sourceProject = Path.Combine(Root, "test", "TestProject");
        var project = XDocument.Load(Path.Combine(sourceProject, "TestProject.csproj"));
        foreach (var import in project.Root!.Elements("Import"))
        {
            var name = import.Attribute("Project")!.Value.EndsWith("Sdk.props", StringComparison.Ordinal) ? "Sdk.props" : "Sdk.targets";
            import.SetAttributeValue("Project", Path.Combine(Root, "src", "MSBuild.Sdk.SqlProj", "Sdk", name));
        }

        // The entity relationship diagram is unrelated to these tests and slows the build.
        project.Root.Element("PropertyGroup")!.Element("GenerateEntityRelationshipDiagram")!.Value = "false";
        if (sqlServerVersion != null)
        {
            project.Root.Element("PropertyGroup")!.SetElementValue("SqlServerVersion", sqlServerVersion);
        }

        var projectPath = Path.Combine(directory.FullName, "TestProject.csproj");
        project.Save(projectPath);
        Directory.CreateDirectory(Path.Combine(directory.FullName, "Tables"));
        File.Copy(Path.Combine(sourceProject, "Tables", "MyTable.sql"), Path.Combine(directory.FullName, "Tables", "MyTable.sql"));
        return projectPath;
    }

    private static string FindRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MSBuild.Sdk.SqlProj.slnx")))
        {
            root = root.Parent;
        }
        root.ShouldNotBeNull("Run these tests from a repository checkout.");
        return root.FullName;
    }

    private static string BuildToolProperty()
    {
        var configuration = typeof(RepositoryWorkspace).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return $"-p:DacpacToolExe={Path.Combine(Root, "src", "DacpacTool", "bin", configuration, "net10.0", "DacpacTool.dll")}";
    }
}
