using System.IO;
using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    [CliCommand(Description = "Deploy a SQL Server Data-Tier Application Framework package (dacpac)", 
                Name = "deploy",
                NameCasingConvention = CliNameCasingConvention.CamelCase)]
    internal sealed class DeployOptions : BaseOptions
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        [CliOption(Description = "Path to the .dacpac package to deploy", Required = true)]
        public FileInfo Input { get; set; }
        
        [CliOption(Description = "Name of the server to deploy the package to", Required = true)]
        public string TargetServerName { get; set; }
        
        [CliOption(Description = "Port number to connect on (leave blank for default)", Name = "--targetPort")]
        public int? TargetPort { get; set; }
        
        [CliOption(Description = "Name of the database to deploy the package to", Required = true)]
        public string TargetDatabaseName { get; set; }
        
        [CliOption(Description = "Username used to connect to the target server, using SQL Server authentication")]
        public string TargetUser { get; set; }
        
        [CliOption(Description = "Password used to connect to the target server, using SQL Server authentication")]
        public string TargetPassword { get; set; }
        
        [CliOption(Description = "Properties used to control the deployment")]
        public string[] Property { get; set; }
        
        [CliOption(Description = "SqlCmdVariable(s) and their associated values, separated by an equals sign", Name = "--sqlcmdvar")]
        public string[] SqlCmdVar { get; set; }
        
        [CliOption(Description = "Whether to run pre- and postdeployment scripts from references", Name = "--runScriptsFromReferences")]
        public bool RunScriptsFromReferences { get; set; }
        
        [CliOption(Description = "Encrypt the connection, defaults to false")]
        public bool Encrypt { get; set; }
        
        public int Run()
        {
            return Program.DeployDacpac(this);
        }
    }
}
