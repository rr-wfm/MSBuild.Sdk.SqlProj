using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;
using DotMake.CommandLine;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace MSBuild.Sdk.SqlProj.DacpacToolFramework
{
    [CliCommand(Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)", 
            Children = new[] { typeof(BuildOptions) })]
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

            // Build package and save to disk
            using var buildResult = PackageBuilder.BuildAndSavePackage(new ActualConsole(), options);

            if (buildResult.HasValidationErrors)
            {
                return 1;
            }

            return 0;
        }
        
        [Conditional("DEBUG")]
        internal static void WaitForDebuggerToAttach(BaseOptions options)
        {
    #if DEBUG
            if (options.Debug)
            {
                var processId = Process.GetCurrentProcess().Id;
                Console.WriteLine($"Waiting for debugger to attach ({processId})");
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
