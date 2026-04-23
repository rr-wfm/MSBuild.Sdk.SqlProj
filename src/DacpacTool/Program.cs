using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotMake.CommandLine;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [CliCommand(Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)", 
                Children = new[] { typeof(BuildOptions), typeof(InspectOptions), typeof(DeployOptions) })]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DotMake.CommandLine via Cli.RunAsync<RootCommand>.")]    
    internal sealed class RootCommand
    {
    }

    sealed class Program
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Not called repeatedly")]
        static async Task<int> Main(string[] args)
        {
            return await Cli.RunAsync<RootCommand>(args, new CliSettings
            {
                // Configure settings if needed
            }).ConfigureAwait(false);
        }

        internal static async Task<int> BuildDacpac(BuildOptions options)
        {
            // Wait for a debugger to attach
            WaitForDebuggerToAttach(options);
            var versionChecker = new VersionChecker(new ActualConsole(), new VersionProvider());

            await versionChecker.CheckForPackageUpdateAsync().ConfigureAwait(false);

            // Build package and save to disk
            var buildResult = PackageBuilder.BuildAndSavePackage(new ActualConsole(), options);

            if (buildResult.HasValidationErrors)
            {
                return 1;
            }

            // Add predeployment and postdeployment scripts (must happen after SaveToDisk)
            var packageHelper = new PackageHelper(new ActualConsole());

            packageHelper.AddPreDeploymentScript(options.PreDeploy, options.Output);            
            packageHelper.AddPostDeploymentScript(options.PostDeploy, options.Output);

            if (options.GenerateCreateScript)
            {
                var deployOptions = options.ExtractDeployOptions();
                packageHelper.GenerateCreateScript(options.Output, options.TargetDatabaseName ?? options.Name, deployOptions);
            }

            if (options.RunCodeAnalysis)
            {
                var analyzer = new PackageAnalyzer(new ActualConsole(), options.CodeAnalysisRules);

                analyzer.Analyze(buildResult.Model, options.Output, options.CodeAnalysisAssemblies ?? Array.Empty<FileInfo>());
            }

            if (options.GenerateErDiagram)
            {
                var diagramBuilder = new MermaidDiagramBuilder(new ActualConsole());

                diagramBuilder.BuildErDiagrams(buildResult.Model, options.TargetDatabaseName ?? options.Name, options.ErDiagramConfig);
            }

            return 0;
        }

        internal static int InspectIncludes(InspectOptions options)
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

        internal static int DeployDacpac(DeployOptions options)
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
                Console.WriteLine($"ERROR: An error occurred while validating arguments: {ex.Message}");
                return 1;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: An error occurred during deployment: {ex.Message}");
                return 1;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        [Conditional("DEBUG")]
        internal static void WaitForDebuggerToAttach(BaseOptions options)
        {
#if DEBUG
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
#endif
        }
    }
}
