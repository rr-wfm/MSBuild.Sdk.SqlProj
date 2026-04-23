using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Text;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace DacpacToolFramework
{
    internal static class Program
    {
        /// <summary>
        /// Adds a <c>CREATE ASSEMBLY</c> object for each SQL CLR assembly (.dll) passed in via
        /// <c>-asm</c> to an existing dacpac produced by DacpacTool. The dacpac is rewritten in place.
        ///
        /// Usage:
        ///   DacpacToolFramework.exe add-assemblies
        ///       -i &lt;input.dacpac&gt;
        ///       [-ps &lt;Safe|ExternalAccess|Unsafe&gt;]
        ///       [-asm &lt;path-to-dll&gt; ...]
        ///       [--debug]
        /// </summary>
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0 || !string.Equals(args[0], "add-assemblies", StringComparison.OrdinalIgnoreCase))
            {
                WriteUsage();
                return 1;
            }

            string inputPath = null;
            var defaultPermissionSet = AssemblyPermissionSet.Safe;
            var assemblies = new List<string>();
            var deferredSqlFiles = new List<string>();
            var waitForDebugger = false;
            var trustInPreDeploy = false;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                    case "--input":
                        inputPath = args[++i];
                        break;
                    case "-ps":
                    case "--permissionset":
                        defaultPermissionSet = (AssemblyPermissionSet)Enum.Parse(typeof(AssemblyPermissionSet), args[++i], ignoreCase: true);
                        break;
                    case "-asm":
                    case "--assembly":
                        assemblies.Add(args[++i]);
                        break;
                    case "-sql":
                    case "--sql-file":
                        deferredSqlFiles.Add(args[++i]);
                        break;
                    case "-tip":
                    case "--trustinpredeploy":
                        trustInPreDeploy = true;
                        break;
                    case "--debug":
                        waitForDebugger = true;
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[i]}");
                        return 1;
                }
            }

            if (waitForDebugger)
            {
                Console.Out.WriteLine($"Waiting for debugger to attach to process {Process.GetCurrentProcess().Id}...");
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                Console.Error.WriteLine("Missing required argument: -i <input.dacpac>");
                return 1;
            }
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Input dacpac not found: {inputPath}");
                return 1;
            }
            if (assemblies.Count == 0 && deferredSqlFiles.Count == 0)
            {
                Console.Out.WriteLine("No assemblies or deferred SQL files supplied; nothing to do.");
                return 0;
            }

            try
            {
                AddAssembliesToDacpac(inputPath, defaultPermissionSet, assemblies, deferredSqlFiles);
                if (trustInPreDeploy && assemblies.Count > 0)
                {
                    InjectTrustedAssemblyPreDeployScript(inputPath, assemblies);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                if (waitForDebugger)
                {
                    Console.Error.WriteLine(ex);
                }
                return 1;
            }
        }

        private static void AddAssembliesToDacpac(
            string inputPath,
            AssemblyPermissionSet defaultPermissionSet,
            List<string> assemblies,
            List<string> deferredSqlFiles)
        {
            Console.Out.WriteLine($"Adding {assemblies.Count} assembly reference(s) and {deferredSqlFiles.Count} deferred SQL file(s) to '{inputPath}'");

            PackageMetadata metadata;
            using (var package = DacPackage.Load(inputPath, DacSchemaModelStorageType.Memory))
            {
                metadata = new PackageMetadata
                {
                    Name = package.Name,
                    Version = package.Version?.ToString() ?? "1.0.0.0",
                    Description = package.Description,
                };
            }

            using (var model = TSqlModel.LoadFromDacpac(
                inputPath,
                new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: false)))
            {
                foreach (var assemblyPath in assemblies)
                {
                    if (!File.Exists(assemblyPath))
                    {
                        throw new FileNotFoundException($"Assembly file not found: {assemblyPath}", assemblyPath);
                    }

                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                    var script = BuildCreateAssemblyScript(assemblyName, assemblyPath, defaultPermissionSet);

                    Console.Out.WriteLine($"Adding CREATE ASSEMBLY [{assemblyName}] from '{assemblyPath}'");
                    model.AddOrUpdateObjects(script, assemblyName + ".sql", new TSqlObjectOptions());
                }

                foreach (var sqlFile in deferredSqlFiles)
                {
                    if (!File.Exists(sqlFile))
                    {
                        throw new FileNotFoundException($"Deferred SQL file not found: {sqlFile}", sqlFile);
                    }

                    Console.Out.WriteLine($"Re-adding deferred SQL file '{sqlFile}'");
                    model.AddOrUpdateObjects(File.ReadAllText(sqlFile), sqlFile, new TSqlObjectOptions());
                }

                DacPackageExtensions.BuildPackage(inputPath, model, metadata, new PackageOptions());
            }

            Console.Out.WriteLine($"Updated dacpac '{inputPath}'");
        }

        private static string BuildCreateAssemblyScript(string assemblyName, string assemblyPath, AssemblyPermissionSet permissionSet)
        {
            var bytes = File.ReadAllBytes(assemblyPath);
            var hex = new StringBuilder(bytes.Length * 2 + 2);
            hex.Append("0x");
            foreach (var b in bytes)
            {
                hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }

            var permissionSetClause = permissionSet switch
            {
                AssemblyPermissionSet.Safe => "SAFE",
                AssemblyPermissionSet.ExternalAccess => "EXTERNAL_ACCESS",
                AssemblyPermissionSet.Unsafe => "UNSAFE",
                _ => "SAFE",
            };

            return
                $"CREATE ASSEMBLY [{assemblyName.Replace("]", "]]")}]\r\n" +
                $"    AUTHORIZATION [dbo]\r\n" +
                $"    FROM {hex}\r\n" +
                $"    WITH PERMISSION_SET = {permissionSetClause};\r\n";
        }

        private static void WriteUsage()
        {
            Console.Error.WriteLine("Usage: DacpacToolFramework.exe add-assemblies -i <input.dacpac> [-ps Safe|ExternalAccess|Unsafe] [-asm <dll> ...] [-sql <deferred.sql> ...] [--trustinpredeploy] [--debug]");
        }

        /// <summary>
        /// Prepends a block of T-SQL to the dacpac's pre-deployment script that registers each
        /// referenced SQL CLR assembly in <c>sys.trusted_assemblies</c> via
        /// <c>sys.sp_add_trusted_assembly</c> when it is not already trusted. This runs before
        /// the model deployment performs CREATE ASSEMBLY, allowing CLR strict security to be
        /// satisfied without manual operator intervention.
        /// </summary>
        private static void InjectTrustedAssemblyPreDeployScript(string inputPath, List<string> assemblies)
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

            Console.Out.WriteLine($"Injected trusted-assembly pre-deployment script for {assemblies.Count} assembly reference(s).");
        }

        private static string BuildTrustAssembliesScript(List<string> assemblies)
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
                var safeName = assemblyName.Replace("'", "''");

                sb.AppendLine($"-- Trust assembly: {assemblyName}");
                sb.AppendLine($"DECLARE @name sysname = N'{safeName}';");
                sb.AppendLine("DECLARE @description nvarchar(4000) = @name;");
                sb.AppendLine($"DECLARE @hash varbinary(64) = {hashHex};");
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
    }

    internal enum AssemblyPermissionSet
    {
        Safe,
        ExternalAccess,
        Unsafe,
    }
}
