using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var buildCommand = new Command("build")
            {
                new Option<string>(new string[] { "--name", "-n" }, "Name of the package"),
                new Option<string>(new string[] { "--version", "-v" }, "Version of the package"),
                new Option<FileInfo>(new string[] { "--output", "-o" }, "Filename of the output package"),
                new Option<SqlServerVersion>(new string[] { "--sqlServerVersion", "-sv" }, () => SqlServerVersion.Sql150, description: "Target version of the model"), 
                new Option<FileInfo[]>(new string[] { "--input", "-i" }, "Input file name(s)"),
                new Option<FileInfo[]>(new string[] { "--reference", "-r" }, "Reference(s) to include"),
                new Option<FileInfo>(new string[] { "--predeploy" }, "Filename of optional pre-deployment script"),
                new Option<FileInfo>(new string[] { "--postdeploy" }, "Filename of optional post-deployment script"),
                new Option<string[]>(new string[] { "--property", "-p" }, "Properties to be set on the model"),
                new Option<string[]>(new string[] { "--sqlcmdvar", "-sc" }, "SqlCmdVariable(s) to include"),
            };
            buildCommand.Handler = CommandHandler.Create<BuildOptions>(BuildDacpac);

            var deployCommand = new Command("deploy")
            {
                new Option<FileInfo>(new string[] { "--input", "-i" }, "Path to the .dacpac package to deploy"),
                new Option<string>(new string[] { "--targetServer", "-ts" }, "Name of the server to deploy the package to"),
                new Option<string>(new string[] { "--targetDatabaseName", "-td" }, "Name of the database to deploy the package to"),
                new Option<string>(new string[] { "--username", "-u" }, "Username used to connect to the target server"),
                new Option<string[]>(new string[] { "--property", "-p" }, "Properties used to control the deployment")
            };
            deployCommand.Handler = CommandHandler.Create<DeployOptions>(DeployDacpac);

            var rootCommand = new RootCommand { buildCommand, deployCommand };
            rootCommand.Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)";

            return await rootCommand.InvokeAsync(args);
        }

        private static int BuildDacpac(BuildOptions options)
        {
            // Set metadata for the package
            using var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata(options.Name, options.Version);

            // Set properties on the model (if defined)
            if (options.Property != null)
            {
                foreach (var propertyValue in options.Property)
                {
                    string[] keyValuePair = propertyValue.Split('=', 2);
                    packageBuilder.SetProperty(keyValuePair[0], keyValuePair[1]);
                }
            }

            // Build the empty model for the target SQL Server version
            packageBuilder.UsingVersion(options.SqlServerVersion);

            // Add references to the model
            if (options.Reference != null)
            {
                foreach (var referenceFile in options.Reference)
                {
                    packageBuilder.AddReference(referenceFile);
                }
            }

            // Add SqlCmdVariables to the package (if defined)
            if (options.SqlCmdVar != null)
            {
                packageBuilder.AddSqlCmdVariables(options.SqlCmdVar);
            }

            // Add input files
            if (options.Input != null)
            {
                foreach (var inputFile in options.Input)
                {
                    packageBuilder.AddInputFile(inputFile);
                }
            }

            // Validate the model
            if (!packageBuilder.ValidateModel())
            {
                return 1;
            }

            // Save the package to disk
            packageBuilder.SaveToDisk(options.Output);

            // Add predeployment and postdeployment scripts (must happen after SaveToDisk)
            packageBuilder.AddPreDeploymentScript(options.PreDeploy, options.Output);
            packageBuilder.AddPostDeploymentScript(options.PostDeploy, options.Output);

            return 0;
        }

        private static int DeployDacpac(DeployOptions options)
        {
            using var deployer = new PackageDeployer(new ActualConsole());
            deployer.LoadPackage(options.Input);

            if (options.Property != null)
            {
                foreach (var propertyValue in options.Property)
                {
                    string[] keyValuePair = propertyValue.Split('=', 2);
                    deployer.SetProperty(keyValuePair[0], keyValuePair[1]);
                }
            }

            deployer.UseTargetServer(options.TargetServer);
            
            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                deployer.UseSqlAuthentication(options.Username, null);
            }
            else
            {
                deployer.UseWindowsAuthentication();
            }

            deployer.Deploy(options.TargetDatabaseName);
            return 0;
        }
    }
}
