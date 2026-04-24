using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Globalization;
using DotMake.CommandLine;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
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

            // When a base dacpac is provided (built by DacpacToolFramework), skip building
            BuildPackageResult buildResult;
            if (options.BaseDacpac != null)
            {
                buildResult = null;
                var basePath = Path.GetFullPath(options.BaseDacpac.FullName);
                var outputPath = Path.GetFullPath(options.Output.FullName);

                // Copy the base dacpac to the output location if they differ
                if (!string.Equals(basePath, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(basePath, outputPath, overwrite: true);
                }
            }
            else
            {
                // Build package and save to disk
                buildResult = PackageBuilder.BuildAndSavePackage(new ActualConsole(), options);

                if (buildResult.HasValidationErrors)
                {
                    return 1;
                }
            }

            // Add predeployment and postdeployment scripts (must happen after SaveToDisk)
            var packageHelper = new PackageHelper(new ActualConsole());

            packageHelper.AddPreDeploymentScript(options.PreDeploy, options.Output);            
            packageHelper.AddPostDeploymentScript(options.PostDeploy, options.Output);

            if (options.TrustInPreDeploy)
            {
                // Add assembly references to the model
                if (options.AssemblyReference != null && options.AssemblyReference.Length > 0)
                {
                    InjectTrustedAssemblyPreDeployScript(options.Output.FullName, options.AssemblyReference);
                }
            }

            if (options.GenerateCreateScript)
            {
                var deployOptions = options.ExtractDeployOptions();
                packageHelper.GenerateCreateScript(options.Output, options.TargetDatabaseName ?? options.Name, deployOptions);
            }

            TSqlModel model = null;
            if (options.RunCodeAnalysis)
            {
                model = GetModel(buildResult, options);

                var analyzer = new PackageAnalyzer(new ActualConsole(), options.CodeAnalysisRules);

                analyzer.Analyze(model, options.Output, options.CodeAnalysisAssemblies ?? Array.Empty<FileInfo>());
            }

            if (options.GenerateErDiagram)
            {
                model = model ?? GetModel(buildResult, options);

                var diagramBuilder = new MermaidDiagramBuilder(new ActualConsole());

                diagramBuilder.BuildErDiagrams(model, options.TargetDatabaseName ?? options.Name, options.ErDiagramConfig);
            }

            return 0;
        }

        private static TSqlModel GetModel(BuildPackageResult buildResult, BuildOptions options)
        {
            if (buildResult != null)
            {
                return buildResult.Model;
            }

            if (options.BaseDacpac != null)
            {
                return TSqlModel.LoadFromDacpac(options.BaseDacpac.FullName,
                    new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: false));
            }

            throw new InvalidOperationException("Model not built or passed as BaseDacpac");        
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

        /// <summary>
        /// Prepends a block of T-SQL to the dacpac's pre-deployment script that registers each
        /// referenced SQL CLR assembly in <c>sys.trusted_assemblies</c> via
        /// <c>sys.sp_add_trusted_assembly</c> when it is not already trusted. This runs before
        /// the model deployment performs CREATE ASSEMBLY, allowing CLR strict security to be
        /// satisfied without manual operator intervention.
        /// </summary>
        private static void InjectTrustedAssemblyPreDeployScript(string inputPath, string[] assemblies)
        {
            const string PreDeployPartUri = "/predeploy.sql";

            var trustScript = BuildTrustAssembliesScript(assemblies);

            using (var package = Package.Open(inputPath, FileMode.Open, FileAccess.ReadWrite))
            {
                var partUri = new Uri(PreDeployPartUri, UriKind.Relative);
                string existing = string.Empty;
                if (package.PartExists(partUri))
                {
                    var existingPart = package.GetPart(partUri);
                    using (var stream = existingPart.GetStream(FileMode.Open, FileAccess.Read))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        existing = reader.ReadToEnd();
                    }
                    package.DeletePart(partUri);
                }

                var combined = trustScript;
                if (!string.IsNullOrEmpty(existing))
                {
                    combined = trustScript + existing;
                }

                var part = package.CreatePart(partUri, "text/plain");
                using (var stream = part.GetStream(FileMode.Create, FileAccess.Write))
                {
                    var buffer = Encoding.UTF8.GetBytes(combined);
                    stream.Write(buffer, 0, buffer.Length);
                }
            }

            Console.Out.WriteLine($"Injected trusted-assembly pre-deployment script for {assemblies.Length} assembly reference(s).");
        }

        private static string BuildTrustAssembliesScript(IEnumerable<string> assemblies)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- BEGIN MSBuild.Sdk.SqlProj: trust referenced SQL CLR assemblies");
            sb.AppendLine("IF OBJECT_ID('tempdb..#is_assembly_trusted') IS NOT NULL DROP PROCEDURE #is_assembly_trusted;");
            sb.AppendLine("GO");
            sb.AppendLine("CREATE PROCEDURE #is_assembly_trusted @hash varbinary(64)");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    RETURN IIF(EXISTS (SELECT * FROM sys.trusted_assemblies WHERE [hash] = @hash), 1, 0);");
            sb.AppendLine("END");
            sb.AppendLine("GO");

            foreach (var assemblyPath in assemblies)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                // Compute the SHA-512 hash at build time so the predeploy script only carries the
                // 64-byte hash literal instead of the full assembly bytes (those are already
                // embedded once in the corresponding CREATE ASSEMBLY ... FROM 0x... statement).
                var hashHex = ToHexLiteral(ComputeSha512(assemblyPath));
                var safeName = assemblyName.Replace("'", "''", StringComparison.OrdinalIgnoreCase);

                sb.AppendLine(CultureInfo.InvariantCulture, $"-- Trust assembly: {assemblyName}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"DECLARE @name sysname = N'{safeName}';");
                sb.AppendLine("DECLARE @description nvarchar(4000) = @name;");
                sb.AppendLine(CultureInfo.InvariantCulture, $"DECLARE @hash varbinary(64) = {hashHex};");
                sb.AppendLine("DECLARE @is_assembly_trusted bit;");
                sb.AppendLine("EXEC @is_assembly_trusted = #is_assembly_trusted @hash;");
                sb.AppendLine("IF @is_assembly_trusted = 1");
                sb.AppendLine("BEGIN");
                sb.AppendLine("    PRINT 'Assembly ' + @name + ' already trusted';");
                sb.AppendLine("END");
                sb.AppendLine("ELSE");
                sb.AppendLine("BEGIN");
                sb.AppendLine("    PRINT 'Assembly ' + @name + ' not trusted yet, trusting...';");
                sb.AppendLine("    EXEC sys.sp_add_trusted_assembly @hash = @hash, @description = @description;");
                sb.AppendLine("    EXEC @is_assembly_trusted = #is_assembly_trusted @hash;");
                sb.AppendLine("    IF @is_assembly_trusted = 0");
                sb.AppendLine("    BEGIN");
                sb.AppendLine("        DECLARE @msg nvarchar(max) = CONCAT('Trusting the assembly ', @name, ' failed. This may be caused by a lack of permissions. Execute the following command manually on the server to trust the assembly, then re-run the pipeline. declare @description nvarchar(4000) = ''', @description, '''; exec sys.sp_add_trusted_assembly @hash = ', CONVERT(varchar(max), @hash, 1), ', @description = @description');");
                sb.AppendLine("        ;THROW 50000, @msg, 1;");
                sb.AppendLine("    END");
                sb.AppendLine("    PRINT 'Assembly ' + @name + ' trusted';");
                sb.AppendLine("END");
                sb.AppendLine("GO");
            }

            sb.AppendLine("-- END MSBuild.Sdk.SqlProj: trust referenced SQL CLR assemblies");
            return sb.ToString();
        }

        private static byte[] ComputeSha512(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA512.Create();
            using var stream = File.OpenRead(filePath);
            return sha.ComputeHash(stream);
        }

        private static string ToHexLiteral(byte[] bytes)
        {
            var hex = new StringBuilder(bytes.Length * 2 + 2);
            hex.Append("0x");
            foreach (var b in bytes)
            {
                hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            return hex.ToString();
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
