﻿using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    sealed class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Not called repeatedly")]
        static async Task<int> Main(string[] args)
        {
            var buildCommand = new Command("build")
            {
                new Option<string>(new string[] { "--name", "-n" }, "Name of the package") { IsRequired = true },
                new Option<string>(new string[] { "--version", "-v" }, "Version of the package"),
                new Option<FileInfo>(new string[] { "--output", "-o" }, "Filename of the output package"),
                new Option<SqlServerVersion>(new string[] { "--sqlServerVersion", "-sv" }, () => SqlServerVersion.Sql150, description: "Target version of the model"),
                new Option<FileInfo>(new string[] { "--inputfile", "-i" }, "Text file listing all input files"),
                new Option<string[]>(new string[] { "--reference", "-r" }, "Reference(s) to include"),
                new Option<FileInfo>(new string[] { "--predeploy" }, "Filename of optional pre-deployment script"),
                new Option<FileInfo>(new string[] { "--postdeploy" }, "Filename of optional post-deployment script"),
                new Option<FileInfo>(new string[] { "--refactorlog" }, "Filename of optional refactor log script"),
                new Option<string[]>(new string[] { "--buildproperty", "-bp" }, "Build properties to be set on the model"),
                new Option<string[]>(new string[] { "--deployproperty", "-dp" }, "Deploy properties to be set for the create script"),
                new Option<string[]>(new string[] { "--sqlcmdvar", "-sc" }, "SqlCmdVariable(s) to include"),
                
                new Option<bool>(new string[] { "--runcodeanalysis", "-an" }, "Run static code analysis"),
                new Option<string>(new string[] { "--codeanalysisrules", "-ar" }, "List of rules to suppress in format '-Microsoft.Rules.Data.SR0001;-Microsoft.Rules.Data.SR0008'"),
                new Option<FileInfo[]>(new string[] { "--codeanalysisassemblies", "-aa" }, "Custom code analysis rule assemblies to use"),

                new Option<bool>(new string[] { "--warnaserror" }, "Treat T-SQL Warnings As Errors"),
                new Option<bool>(new string[] { "--generatecreatescript", "-gcs" }, "Generate create script for package"),
                new Option<bool>(new string[] { "--includecompositeobjects", "-ico" }, "Include referenced, external elements that also compose the source model"),
                new Option<string>(new string[] { "--targetdatabasename", "-tdn" }, "Name of the database to use in the generated create script"),
                new Option<string>(new string[] { "--suppresswarnings", "-spw" }, "Warning(s) to suppress"),
                new Option<FileInfo>(new string[] { "--suppresswarningslistfile", "-spl" }, "Filename for warning(s) to suppress for particular files"),
#if DEBUG
                new Option<bool>(new string[] { "--debug" }, "Waits for a debugger to attach")
#endif
            };
            buildCommand.Handler = CommandHandler.Create<BuildOptions>(async (buildOptions) =>
            {
                await BuildDacpac(buildOptions).ConfigureAwait(false);
            });

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
                new Option<bool>(new string[] { "--runScriptsFromReferences", "-sff" }, "Whether to run pre- and postdeployment scripts from references"),
                new Option<bool>(new string[] { "--encrypt", "-e" }, "Encrypt the connection, defaults to false"),
#if DEBUG
                new Option<bool>(new string[] { "--debug" }, "Waits for a debugger to attach")
#endif
            };
            deployCommand.Handler = CommandHandler.Create<DeployOptions>(DeployDacpac);

            var rootCommand = new RootCommand { buildCommand, collectIncludesCommand, deployCommand };
            rootCommand.Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)";

            var processed = rootCommand.Parse(args);
            return await processed.InvokeAsync().ConfigureAwait(false);
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
