using System.IO;
using DotMake.CommandLine;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used a DTO")]
    [CliCommand(Description = "Build a SQL Server Data-Tier Application package (dacpac)")]
    public class BuildOptions : BaseOptions
    {
        [CliOption(Required = true, Description = "Name of the package")]
        public string Name { get; set; }

        [CliOption(Description = "Version of the package")]
        public string Version { get; set; }

        [CliOption(Description = "Filename of the output package")]
        public FileInfo Output { get; set; }

        [CliOption(Description = "Target version of the model")]
        public SqlServerVersion SqlServerVersion { get; set; } = SqlServerVersion.Sql150;

        [CliOption(Description = "Text file listing all input files")]
        public FileInfo InputFile { get; set; }
        
        [CliOption(Description = "Reference(s) to include")]
        public string[] Reference { get; set; }

        [CliOption(Description = "Build properties to be set on the model")]
        public string[] BuildProperty { get; set; }

        [CliOption(Description = "Deploy properties to be set for the create script")]
        public string[] DeployProperty { get; set; }

        [CliOption(Description = "SqlCmdVariable(s) to include")]
        public string[] SqlCmdVar { get; set; }

        [CliOption(Description = "Filename of optional pre-deployment script")]
        public FileInfo PreDeploy { get; set; }

        [CliOption(Description = "Filename of optional post-deployment script")]
        public FileInfo PostDeploy { get; set; }

        [CliOption(Description = "Filename of optional refactor log script")]
        public FileInfo RefactorLog { get; set; }

        [CliOption(Description = "Run static code analysis")]
        public bool RunCodeAnalysis { get; set; }

        [CliOption(Description = "List of rules to suppress in format '-Microsoft.Rules.Data.SR0001;-Microsoft.Rules.Data.SR0008'")]
        public string CodeAnalysisRules { get; set; }

        [CliOption(Description = "Custom code analysis rule assemblies to use")]
        public FileInfo[] CodeAnalysisAssemblies { get; set; }

        [CliOption(Description = "Treat T-SQL Warnings As Errors")]
        public bool WarnAsError { get; set; }

        [CliOption(Description = "Warning(s) to suppress")]
        public string SuppressWarnings { get; set; }

        [CliOption(Description = "Filename for warning(s) to suppress for particular files")]
        public FileInfo SuppressWarningsListFile { get; set; }

        [CliOption(Description = "Generate create script for package")]
        public bool GenerateCreateScript { get; set; }

        [CliOption(Description = "Name of the database to use in the generated create script")]
        public string TargetDatabaseName { get; set; }
    }
}
