using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

public class BuildAndSavePackageParameters
{
    public string Name { get; private set; }

    public string Version { get; private set; }

    public ICollection<string> BuildProperties { get; private set; }

    public SqlServerVersion SqlServerVersion { get; private set; }

    public ICollection<string> References { get; private set; }

    public ICollection<string> AssemblyReferences { get; private set; }

    public ICollection<string> SqlCmdVars { get; private set; }

    /// <summary>
    /// File containing a line for each file to include in the package. 
    /// </summary>
    public FileInfo InputFile { get; private set; }

    public bool WarnAsError { get; private set; }

    public string SuppressWarnings { get; private set; }

    /// <summary>
    /// File containing a line for each file to suppress warnings for.
    /// </summary>
    public FileInfo SuppressWarningsListFile { get; private set; }

    public FileInfo Output { get; private set; }

    public FileInfo RefactorLog { get; private set; }

    public BuildAndSavePackageParameters(
        string name,
        string version,
        ICollection<string> buildProperties,
        SqlServerVersion sqlServerVersion,
        ICollection<string> references,
        ICollection<string> assemblyReferences,
        ICollection<string> sqlCmdVars,
        FileInfo inputFile,
        bool warnAsError,
        string suppressWarnings,
        FileInfo suppressWarningsListFile,
        FileInfo output,
        FileInfo refactorLog
    )
    {
        Name = name;
        Version = version;
        BuildProperties = buildProperties;
        SqlServerVersion = sqlServerVersion;
        References = references;
        AssemblyReferences = assemblyReferences;
        SqlCmdVars = sqlCmdVars;
        InputFile = inputFile;
        WarnAsError = warnAsError;
        SuppressWarnings = suppressWarnings;
        SuppressWarningsListFile = suppressWarningsListFile;
        Output = output;
        RefactorLog = refactorLog;
    }
}

