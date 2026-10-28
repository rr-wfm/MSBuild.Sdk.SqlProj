using System;
using System.IO;
using System.Reflection;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    internal static class DacpacToolPath
    {
        // Test-host output may contain dependencies for a newer TFM than the standalone CLI.
        internal static string FilePath { get; } = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/DacpacTool/bin",
            typeof(DacpacToolPath).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration,
            "net8.0",
            "DacpacTool.dll"));
    }
}
