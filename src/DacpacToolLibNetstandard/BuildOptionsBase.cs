using DotMake.CommandLine;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

/// <summary>
/// This contains the build options implemented both in .NET Core (DacpacTool) and .NET Framework (DacpacToolFramework).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Needed for command line parsing")]
public class BuildOptionsBase : BaseOptions
{
    [CliOption(Description = "Name of the package", Required = true, Name = "--name", Alias = "-n")]
    public string Name { get; set; }
    
    [CliOption(Description = "Version of the package", Required = false, Name = "--version", Alias = "-v")]
    public string Version { get; set; }
    
    [CliOption(Description = "Filename of the output package", Required = false, Name = "--output", Alias = "-o")]
    public FileInfo Output { get; set; }
    
    [CliOption(Description = "Target version of the model", Required = false, Name = "--sqlServerVersion", Alias = "-sv")]
    public SqlServerVersion SqlServerVersion { get; set; } = SqlServerVersion.Sql160;
    
    [CliOption(Description = "Text file listing all input files", Name = "--inputfile", Required = false, Alias = "-i")]
    public FileInfo InputFile { get; set; }
    
    [CliOption(Description = "Reference(s) to include", Required = false, Name = "--reference", Alias = "-r")]
    public string[] Reference { get; set; }
    
    [CliOption(Description = "Assembly reference(s) to include", Required = false, Name = "--assemblyreference", Alias = "-asm")]
    public string[] AssemblyReference { get; set; }
    
    [CliOption(Description = "Build properties to be set on the model", Name = "--buildproperty", Required = false, Alias = "-bp")]
    public string[] BuildProperty { get; set; }
    
    [CliOption(Description = "SqlCmdVariable(s) to include", Name = "--sqlcmdvar", Required = false, Alias = "-sc")]
    public string[] SqlCmdVar { get; set; }
    
    [CliOption(Description = "Filename of optional refactor log script", Name = "--refactorlog", Required = false)]
    public FileInfo RefactorLog { get; set; }
    
    [CliOption(Description = "Treat T-SQL Warnings As Errors", Name = "--warnaserror", Required = false)]
    public bool WarnAsError { get; set; }
    
    [CliOption(Description = "Warning(s) to suppress", Name = "--suppresswarnings", Required = false, Alias = "-spw")]
    public string SuppressWarnings { get; set; }
    
    [CliOption(Description = "Filename for warning(s) to suppress for particular files", Name = "--suppresswarningslistfile", Required = false, Alias = "-spl")]
    public FileInfo SuppressWarningsListFile { get; set; }
}
