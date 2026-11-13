using System.Reflection;
using System.Xml.Linq;

namespace MSBuild.Sdk.SqlProj.IntegrationTests
{
    internal static class DacpacToolPath
    {
        private static readonly string ProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/DacpacTool"));

        internal static string TargetFramework { get; } = XDocument.Load(Path.Combine(ProjectDirectory, "DacpacTool.csproj"))
            .Root!.Elements("PropertyGroup").Elements("TargetFramework").Single().Value;

        // The test host can target a different framework from the standalone CLI.
        internal static string FilePath { get; } = Path.Combine(
            ProjectDirectory,
            "bin",
            typeof(DacpacToolPath).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration,
            TargetFramework,
            "DacpacTool.dll");
    }
}
