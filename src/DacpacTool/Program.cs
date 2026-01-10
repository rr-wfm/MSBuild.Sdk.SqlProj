using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotMake.CommandLine;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [CliCommand(Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)")]
    sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await Cli.RunAsync<Program>(args).ConfigureAwait(false);
        }

        [CliCommand(Description = "Build a SQL Server Data-Tier Application package (dacpac)", Name = "build")]
        public class BuildCommand : BuildOptions
        {
            public async Task<int> RunAsync()
            {
                return await BuildDacpac(this).ConfigureAwait(false);
            }
        }

        [CliCommand(Description = "Collect includes from pre- and post-deployment scripts", Name = "collect-includes")]
        public class CollectIncludesCommand : InspectOptions
        {
            public int Run()
            {
                return InspectIncludes(this);
            }
        }

        [CliCommand(Description = "Deploy a SQL Server Data-Tier Application package (dacpac)", Name = "deploy")]
        public class DeployCommand : DeployOptions
        {
            public int Run()
            {
                return DeployDacpac(this);
            }
        }

        private static async Task<int> BuildDacpac(BuildOptions options)
        {
            // Wait for a debugger to attach
            WaitForDebuggerToAttach(options);

            using var packageBuilder = new PackageBuilder(new ActualConsole());
            var versionChecker = new VersionChecker(new ActualConsole(), new VersionProvider());

            await versionChecker.CheckForPackageUpdateAsync().ConfigureAwait(false);

            // Set metadata for the package
            packageBuilder.SetMetadata(options.Name, options.Version);

            // Set properties on the model (if defined)
            if (options.BuildProperty != null)
            {
                foreach (var propertyValue in options.BuildProperty)
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
                    string[] referenceDetails = reference.Split(';', 3, StringSplitOptions.RemoveEmptyEntries);
                    if (referenceDetails.Length == 1)
                    {
                        packageBuilder.AddReference(referenceDetails[0]);
                    }
                    if (referenceDetails.Length == 2)
                    {
                        packageBuilder.AddReference(referenceDetails[0], referenceDetails[1]);
                    }
                    else
                    {
                        if (!bool.TryParse(referenceDetails[2], out bool suppressErrorsForMissingDependencies))
                        {
                            throw new ArgumentException(
                                $"Invalid Option for SuppressMissingDependenciesErrors on {referenceDetails[0]}, must be True/False");
                        }
                        packageBuilder.AddReference(referenceDetails[0], referenceDetails[1], suppressErrorsForMissingDependencies);
                    }
                }
            }

            // Add SqlCmdVariables to the package (if defined)
            if (options.SqlCmdVar != null)
            {
                packageBuilder.AddSqlCmdVariables(options.SqlCmdVar);
            }

            var modelExceptions = false;

            // Add input files by iterating through $Project.InputFiles.txt
            if (options.InputFile != null)
            {
                if (options.InputFile.Exists)
                {
#pragma warning disable CA1849 // Call async methods when in an async method - must wait for .NET 6 to be removed
                    foreach (var line in File.ReadLines(options.InputFile.FullName))
                    {
                        FileInfo inputFile = new FileInfo(line); // Validation occurs in AddInputFile
                        if (!packageBuilder.AddInputFile(inputFile))
                        {
                            modelExceptions = true;
                        }
                    }
#pragma warning restore CA1849 // Call async methods when in an async method
                }
                else
                {
                    throw new ArgumentException($"No input files found, missing {options.InputFile.Name}");
                }
            }

            //Add Warnings options
            packageBuilder.TreatTSqlWarningsAsErrors = options.WarnAsError;
            if (options.SuppressWarnings != null)
            {
                packageBuilder.AddWarningsToSuppress(options.SuppressWarnings);
            }

            // Add warnings suppressions for particular files through $Project.WarningsSuppression.txt
            if (options.SuppressWarningsListFile != null)
            {
                if (options.SuppressWarningsListFile.Exists)
                {
#pragma warning disable CA1849 // Call async methods when in an async method
                    foreach (var line in File.ReadLines(options.SuppressWarningsListFile.FullName))
                    {
                        //Checks if there are suppression warnings list
                        var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                        var warningList = (parts.Length > 1) ? parts[1] : null;

                        FileInfo inputFile = new FileInfo(parts[0]); // Validation occurs in AddInputFile
                        packageBuilder.AddFileWarningsToSuppress(inputFile, warningList);
                    }
#pragma warning restore CA1849 // Call async methods when in an async method
                }
            }

            // Validate the model
            if (modelExceptions || !packageBuilder.ValidateModel())
            {
                return 1;
            }

            // Save the package to disk
            packageBuilder.SaveToDisk(options.Output, new PackageOptions() { RefactorLogPath = options.RefactorLog?.FullName });

            // Add predeployment and postdeployment scripts (must happen after SaveToDisk)
            packageBuilder.AddPreDeploymentScript(options.PreDeploy, options.Output);
            packageBuilder.AddPostDeploymentScript(options.PostDeploy, options.Output);

            if (options.GenerateCreateScript)
            {
                var deployOptions = options.ExtractDeployOptions();
                packageBuilder.GenerateCreateScript(options.Output, options.TargetDatabaseName ?? options.Name, deployOptions);
            }

            if (options.RunCodeAnalysis)
            {
                var analyzer = new PackageAnalyzer(new ActualConsole(), options.CodeAnalysisRules);

                analyzer.Analyze(packageBuilder.Model, options.Output, options.CodeAnalysisAssemblies ?? Array.Empty<FileInfo>());
            }

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
                    deployer.SetDeployProperties(options.Property);
                }

                deployer.UseEncrypt(options.Encrypt);

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

                if (options.RunScriptsFromReferences)
                {
                    deployer.RunPreDeploymentScriptFromReferences(options.Input, options.TargetDatabaseName);
                }

                deployer.Deploy(options.Input, options.TargetDatabaseName);

                if (options.RunScriptsFromReferences)
                {
                    deployer.RunPostDeploymentScriptFromReferences(options.Input, options.TargetDatabaseName);
                }

                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"ERROR: An error occured while validating arguments: {ex.Message}");
                return 1;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: An error ocurred during deployment: {ex.Message}");
                return 1;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        [Conditional("DEBUG")]
        private static void WaitForDebuggerToAttach(BaseOptions options)
        {
            if (options.Debug)
            {
                Console.WriteLine($"Waiting for debugger to attach ({Environment.ProcessId})");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Console.WriteLine(@"Debugger attached");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
        }
    }
}
