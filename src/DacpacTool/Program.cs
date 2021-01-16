using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac;
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
                new Option<FileInfo>(new string[] { "--inputfile", "-i" }, "Text file listing all input files"),
                new Option<string[]>(new string[] { "--reference", "-r" }, "Reference(s) to include"),
                new Option<FileInfo>(new string[] { "--predeploy" }, "Filename of optional pre-deployment script"),
                new Option<FileInfo>(new string[] { "--postdeploy" }, "Filename of optional post-deployment script"),
                new Option<FileInfo>(new string[] { "--refactorlog" }, "Filename of optional refactor log script"),
                new Option<string[]>(new string[] { "--property", "-p" }, "Properties to be set on the model"),
                new Option<string[]>(new string[] { "--sqlcmdvar", "-sc" }, "SqlCmdVariable(s) to include"),
#if DEBUG
                new Option<bool>(new string[] { "--debug" }, "Waits for a debugger to attach")
#endif
            };
            buildCommand.Handler = CommandHandler.Create<BuildOptions>(BuildDacpac);

            var collectIncludesCommand = new Command("collect-includes")
            {
                new Option<FileInfo>(new string[] { "--predeploy" }, "Filename of optional pre-deployment script"),
                new Option<FileInfo>(new string[] { "--postdeploy" }, "Filename of optional post-deployment script"),
#if DEBUG
                new Option<bool>(new string[] { "--debug" }, "Waits for a debugger to attach")
#endif
            };
            collectIncludesCommand.Handler = CommandHandler.Create<InspectOptions>(InspectIncludes);

            var deployCommand = new Command("deploy")
            {
                new Option<FileInfo>(new string[] { "--input", "-i" }, "Path to the .dacpac package to deploy"),
                new Option<string>(new string[] { "--targetServerName", "-tsn" }, "Name of the server to deploy the package to"),
                new Option<int>(new string[] { "--targetPort", "-tprt" }, "Port number to connect on (leave blank for default)"),
                new Option<string>(new string[] { "--targetDatabaseName", "-tdn" }, "Name of the database to deploy the package to"),
                new Option<string>(new string[] { "--targetUser", "-tu" }, "Username used to connect to the target server, using SQL Server authentication"),
                new Option<string>(new string[] { "--targetPassword", "-tp" }, "Password used to connect to the target server, using SQL Server authentication"),
                new Option<string[]>(new string[] { "--property", "-p" }, "Properties used to control the deployment"),
                new Option<string[]>(new string[] { "--sqlcmdvar", "-sc" }, "SqlCmdVariable(s) and their associated values, separated by an equals sign."),
#if DEBUG
                new Option<bool>(new string[] { "--debug" }, "Waits for a debugger to attach")
#endif
            };
            deployCommand.Handler = CommandHandler.Create<DeployOptions>(DeployDacpac);

            var rootCommand = new RootCommand { buildCommand, collectIncludesCommand, deployCommand };
            rootCommand.Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)";

            return await rootCommand.InvokeAsync(args);
        }

        private static int BuildDacpac(BuildOptions options)
        {
            // Wait for a debugger to attach
            WaitForDebuggerToAttach(options);

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
                foreach (var reference in options.Reference)
                {
                    string[] referenceDetails = reference.Split(';', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (referenceDetails.Length == 1)
                    {
                        packageBuilder.AddReference(referenceDetails[0]);
                    }
                    else
                    {
                        packageBuilder.AddExternalReference(referenceDetails[0], referenceDetails[1]);
                    }
                }
            }

            // Add SqlCmdVariables to the package (if defined)
            if (options.SqlCmdVar != null)
            {
                packageBuilder.AddSqlCmdVariables(options.SqlCmdVar);
            }

            // Add input files by iterating through $Project.InputFiles.txt
            if (options.InputFile != null)
            {
                if (options.InputFile.Exists)
                {
                    foreach (var line in File.ReadLines(options.InputFile.FullName))
                    {
                        FileInfo inputFile = new FileInfo(line); // Validation occurs in AddInputFile
                        packageBuilder.AddInputFile(inputFile);
                    }
                }
                else 
                {
                    throw new ArgumentException($"No input files found, missing {options.InputFile.Name}");
                }
            }

            // Validate the model
            if (!packageBuilder.ValidateModel())
            {
                return 1;
            }

            // Save the package to disk
            packageBuilder.SaveToDisk(options.Output, new PackageOptions() { RefactorLogPath = options.RefactorLog?.FullName });

            // Add predeployment and postdeployment scripts (must happen after SaveToDisk)
            packageBuilder.AddPreDeploymentScript(options.PreDeploy, options.Output);
            packageBuilder.AddPostDeploymentScript(options.PostDeploy, options.Output);

            return 0;
        }

        private static int InspectIncludes(InspectOptions options)
        {
            var scriptInspector = new ScriptInspector();

            // Add predeployment and postdeployment scripts
            if (options.PreDeploy != null) 
            {
                scriptInspector.AddPreDeploymentScript(options.PreDeploy);
            }
            if (options.PostDeploy != null)
            {
                scriptInspector.AddPostDeploymentScript(options.PostDeploy);
            }

            // Write all included files to stdout
            var includedFiles = scriptInspector.IncludedFiles;
            foreach (var file in includedFiles)
            {
                Console.Out.WriteLine(file);
            }

            return 0;
        }

        private static int DeployDacpac(DeployOptions options)
        {
            // Wait for a debugger to attach
            WaitForDebuggerToAttach(options);

            try
            {
                var deployer = new PackageDeployer(new ActualConsole());

                if (options.Property != null)
                {
                    foreach (var propertyValue in options.Property)
                    {
                        string[] keyValuePair = propertyValue.Split('=', 2);
                        deployer.SetProperty(keyValuePair[0], keyValuePair[1]);
                    }
                }

                if (options.SqlCmdVar != null)
                {
                    foreach (var sqlCmdVar in options.SqlCmdVar)
                    {
                        string[] keyValuePair = sqlCmdVar.Split('=', 2);
                        deployer.SetSqlCmdVariable(keyValuePair[0], keyValuePair[1]);
                    }
                }

                if (options.TargetPort.HasValue)
                {
                    deployer.UseTargetServerAndPort(options.TargetServerName, options.TargetPort.Value);
                }
                else
                {
                    deployer.UseTargetServer(options.TargetServerName);
                }
                
                if (!string.IsNullOrWhiteSpace(options.TargetUser))
                {
                    deployer.UseSqlAuthentication(options.TargetUser, options.TargetPassword);
                }
                else
                {
                    deployer.UseWindowsAuthentication();
                }

                deployer.Deploy(options.Input, options.TargetDatabaseName);
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"ERROR: An error occured while validating arguments: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: An error ocurred during deployment: {ex.Message}");
                return 1;
            }
        }

        [Conditional("DEBUG")]
        private static void WaitForDebuggerToAttach(BaseOptions options)
        {
            if (options.Debug)
            {
                Console.WriteLine("Waiting for debugger to attach");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
                Console.WriteLine("Debugger attached");
            }
        }
    }
}
