using System.IO;
using System.Threading.Tasks;
using DotMake.CommandLine;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used a DTO")]
    [CliCommand(Description = "Build a SQL Server Data-Tier Application Framework package (dacpac)", 
                Name = "build",
                NameCasingConvention = CliNameCasingConvention.CamelCase)]
    public class BuildOptions : BaseOptions
    {
        [CliOption(Description = "Name of the package", Required = true)]
        public string Name { get; set; }
        
        [CliOption(Description = "Version of the package", Required = false)]
        public string? Version { get; set; }
        
        [CliOption(Description = "Filename of the output package", Required = false)]
        public FileInfo? Output { get; set; }
        
        [CliOption(Description = "Target version of the model", Required = false)]
        public SqlServerVersion SqlServerVersion { get; set; } = SqlServerVersion.Sql150;
        
        [CliOption(Description = "Text file listing all input files", Name = "--inputfile", Required = false)]
        public FileInfo? InputFile { get; set; }
        
        [CliOption(Description = "Reference(s) to include", Required = false)]
        public string[]? Reference { get; set; }
        
        [CliOption(Description = "Build properties to be set on the model", Name = "--buildproperty", Required = false)]
        public string[]? BuildProperty { get; set; }
        
        [CliOption(Description = "Deploy properties to be set for the create script", Name = "--deployproperty", Required = false)]
        public string[]? DeployProperty { get; set; }
        
        [CliOption(Description = "SqlCmdVariable(s) to include", Name = "--sqlcmdvar", Required = false)]
        public string[]? SqlCmdVar { get; set; }
        
        [CliOption(Description = "Filename of optional pre-deployment script", Name = "--predeploy", Required = false)]
        public FileInfo? PreDeploy { get; set; }
        
        [CliOption(Description = "Filename of optional post-deployment script", Name = "--postdeploy", Required = false)]
        public FileInfo? PostDeploy { get; set; }
        
        [CliOption(Description = "Filename of optional refactor log script", Name = "--refactorlog", Required = false)]
        public FileInfo? RefactorLog { get; set; }
        
        [CliOption(Description = "Run static code analysis", Name = "--runcodeanalysis", Required = false)]
        public bool RunCodeAnalysis { get; set; }
        
        [CliOption(Description = "List of rules to suppress in format '-Microsoft.Rules.Data.SR0001;-Microsoft.Rules.Data.SR0008'", Name = "--codeanalysisrules", Required = false)]
        public string? CodeAnalysisRules { get; set; }
        
        [CliOption(Description = "Custom code analysis rule assemblies to use", Name = "--codeanalysisassemblies", Required = false)]
        public FileInfo[]? CodeAnalysisAssemblies { get; set; }
        
        [CliOption(Description = "Treat T-SQL Warnings As Errors", Name = "--warnaserror", Required = false)]
        public bool WarnAsError { get; set; }
        
        [CliOption(Description = "Generate create script for package", Name = "--generatecreatescript", Required = false)]
        public bool GenerateCreateScript { get; set; }
        
        [CliOption(Description = "Include referenced, external elements that also compose the source model", Name = "--includecompositeobjects", Required = false)]
        public bool IncludeCompositeObjects { get; set; }
        
        [CliOption(Description = "Name of the database to use in the generated create script", Name = "--targetdatabasename", Required = false)]
        public string? TargetDatabaseName { get; set; }
        
        [CliOption(Description = "Warning(s) to suppress", Name = "--suppresswarnings", Required = false)]
        public string? SuppressWarnings { get; set; }
        
        [CliOption(Description = "Filename for warning(s) to suppress for particular files", Name = "--suppresswarningslistfile", Required = false)]
        public FileInfo? SuppressWarningsListFile { get; set; }
        
        public async Task<int> RunAsync()
        {
            return await Program.BuildDacpac(this).ConfigureAwait(false);
        }
    }
}
