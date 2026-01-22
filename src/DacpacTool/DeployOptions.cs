using System.IO;
using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    [CliCommand(Description = "Deploy a SQL Server Data-Tier Application Framework package (dacpac)", 
                Name = "deploy")]
    internal sealed class DeployOptions : BaseOptions
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        [CliOption(Description = "Path to the .dacpac package to deploy", Required = true, Name = "--input", Alias = "-i")]
        public FileInfo Input { get; set; }
        
        [CliOption(Description = "Name of the server to deploy the package to", Required = true, Name = "--targetServerName", Alias = "-tsn")]
        public string TargetServerName { get; set; }
        
        [CliOption(Description = "Port number to connect on (leave blank for default)", Name = "--targetPort", Required = false, Alias = "-tprt")]
        public int? TargetPort { get; set; }
        
        [CliOption(Description = "Name of the database to deploy the package to", Required = true, Name = "--targetDatabaseName", Alias = "-tdn")]
        public string TargetDatabaseName { get; set; }
        
        [CliOption(Description = "Username used to connect to the target server, using SQL Server authentication", Required = false, Name = "--targetUser", Alias = "-tu")]
        public string TargetUser { get; set; }
        
        [CliOption(Description = "Password used to connect to the target server, using SQL Server authentication", Required = false, Name = "--targetPassword", Alias = "-tp")]
        public string TargetPassword { get; set; }
        
        [CliOption(Description = "Properties used to control the deployment", Required = false, Name = "--property", Alias = "-p")]
        public string[] Property { get; set; }
        
        [CliOption(Description = "SqlCmdVariable(s) and their associated values, separated by an equals sign", Name = "--sqlcmdvar", Required = false, Alias = "-sc")]
        public string[] SqlCmdVar { get; set; }
        
        [CliOption(Description = "Whether to run pre- and postdeployment scripts from references", Name = "--runScriptsFromReferences", Required = false, Alias = "-sff")]
        public bool RunScriptsFromReferences { get; set; }
        
        [CliOption(Description = "Encrypt the connection, defaults to false", Required = false, Name = "--encrypt", Alias = "-e")]
        public bool Encrypt { get; set; }
        
        public int Run()
        {
            return Program.DeployDacpac(this);
        }
    }
}
