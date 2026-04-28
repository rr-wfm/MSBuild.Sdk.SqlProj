using System.IO;
using System.Threading.Tasks;
using DotMake.CommandLine;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// This inherits from <see cref="BuildOptionsBase"/> which contains the options also implemented in .NET Framework DacpacTool (DacpacToolFramework).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used a DTO")]
    [CliCommand(Description = "Build a SQL Server Data-Tier Application Framework package (dacpac)", 
                Name = "build")]
    public class BuildOptions : BuildOptionsBase
    {
        [CliOption(Description = "Deploy properties to be set for the create script", Name = "--deployproperty", Required = false, Alias = "-dp")]
        public string[] DeployProperty { get; set; }
        
        [CliOption(Description = "Filename of optional pre-deployment script", Name = "--predeploy", Required = false)]
        public FileInfo PreDeploy { get; set; }
        
        [CliOption(Description = "Filename of optional post-deployment script", Name = "--postdeploy", Required = false)]
        public FileInfo PostDeploy { get; set; }
        
        [CliOption(Description = "Run static code analysis", Name = "--runcodeanalysis", Required = false, Alias = "-an")]
        public bool RunCodeAnalysis { get; set; }
        
        [CliOption(Description = "List of rules to suppress in format '-Microsoft.Rules.Data.SR0001;-Microsoft.Rules.Data.SR0008'", Name = "--codeanalysisrules", Required = false, Alias = "-ar")]
        public string CodeAnalysisRules { get; set; }
        
        [CliOption(Description = "Custom code analysis rule assemblies to use", Name = "--codeanalysisassemblies", Required = false, Alias = "-aa")]
        public FileInfo[] CodeAnalysisAssemblies { get; set; }
        
        [CliOption(Description = "Generate create script for package", Name = "--generatecreatescript", Required = false, Alias = "-gcs")]
        public bool GenerateCreateScript { get; set; }
        
        [CliOption(Description = "Name of the database to use in the generated create script", Name = "--targetdatabasename", Required = false, Alias = "-tdn")]
        public string TargetDatabaseName { get; set; }

        [CliOption(Description = "Generate ER diagram from the dacpac", Name = "--generateerdiagram", Required = false, Alias = "-ged")]
        public bool GenerateErDiagram { get; set; }

        [CliOption(Description = "JSON config file for ER diagram generation", Name = "--erdiagramconfig", Required = false, Alias = "-edc")]
        public FileInfo[] ErDiagramConfig { get; set; }

        [CliOption(Description = "Path to an existing dacpac built by DacpacToolFramework (skips model building)", Name = "--basedacpac", Required = false)]
        public FileInfo BaseDacpac { get; set; }

        [CliOption(Description = "To add code is added to the pre-deployment script to trust each referenced SQL CLR assembly (via sys.sp_add_trusted_assembly) before CREATE ASSEMBLY runs.", Name = "--trustinpredeploy", Required = false)]
        public bool TrustInPreDeploy { get; set; }

        public async Task<int> RunAsync()
        {
            return await Program.BuildDacpac(this).ConfigureAwait(false);
        }
    }
}
