using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace DacpacToolFramework
{
    internal static class Program
    {
        /// <summary>
        /// Builds a .dacpac that contains a <c>CREATE ASSEMBLY</c> object for each
        /// SQL CLR assembly (.dll) passed in via <c>-asm</c>. The resulting dacpac is
        /// intended to be loaded as the base model by DacpacTool, which continues the
        /// build with the project's .sql content.
        ///
        /// Usage:
        ///   DacpacToolFramework.exe build-assemblies
        ///       -o &lt;output.dacpac&gt;
        ///       -n &lt;name&gt;
        ///       [-sv &lt;SqlServerVersion&gt;]
        ///       [-ps &lt;Safe|ExternalAccess|Unsafe&gt;]
        ///       [-asm &lt;path-to-dll&gt; ...]
        ///       [--debug]
        /// </summary>
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0 || !string.Equals(args[0], "build-assemblies", StringComparison.OrdinalIgnoreCase))
            {
                WriteUsage();
                return 1;
            }

            string outputPath = null;
            string name = null;
            string version = "1.0.0";
            var sqlServerVersion = SqlServerVersion.Sql160;
            var defaultPermissionSet = AssemblyPermissionSet.Safe;
            var assemblies = new List<string>();
            var waitForDebugger = false;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-o":
                    case "--output":
                        outputPath = args[++i];
                        break;
                    case "-n":
                    case "--name":
                        name = args[++i];
                        break;
                    case "-v":
                    case "--version":
                        version = args[++i];
                        break;
                    case "-sv":
                    case "--sqlserverversion":
                        sqlServerVersion = (SqlServerVersion)Enum.Parse(typeof(SqlServerVersion), args[++i], ignoreCase: true);
                        break;
                    case "-ps":
                    case "--permissionset":
                        defaultPermissionSet = (AssemblyPermissionSet)Enum.Parse(typeof(AssemblyPermissionSet), args[++i], ignoreCase: true);
                        break;
                    case "-asm":
                    case "--assembly":
                        assemblies.Add(args[++i]);
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

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.Error.WriteLine("Missing required argument: -o <output.dacpac>");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine("Missing required argument: -n <name>");
                return 1;
            }

            try
            {
                BuildAssembliesDacpac(outputPath, name, version, sqlServerVersion, defaultPermissionSet, assemblies);
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

        private static void BuildAssembliesDacpac(
            string outputPath,
            string name,
            string version,
            SqlServerVersion sqlServerVersion,
            AssemblyPermissionSet defaultPermissionSet,
            List<string> assemblies)
        {
            Console.Out.WriteLine($"Building assembly dacpac '{outputPath}' for SQL Server version {sqlServerVersion} with {assemblies.Count} assembly reference(s)");

            using (var model = new TSqlModel(sqlServerVersion, new TSqlModelOptions()))
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

                var outputFile = new FileInfo(outputPath);
                if (outputFile.Directory != null && !outputFile.Directory.Exists)
                {
                    outputFile.Directory.Create();
                }
                if (outputFile.Exists)
                {
                    outputFile.Delete();
                }

                var metadata = new PackageMetadata { Name = name, Version = version };
                DacPackageExtensions.BuildPackage(outputFile.FullName, model, metadata, new PackageOptions());
            }

            Console.Out.WriteLine($"Wrote assembly dacpac to '{outputPath}'");
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
            Console.Error.WriteLine("Usage: DacpacToolFramework.exe build-assemblies -o <output.dacpac> -n <name> [-sv <SqlServerVersion>] [-ps Safe|ExternalAccess|Unsafe] [-asm <dll> ...] [--debug]");
        }
    }

    internal enum AssemblyPermissionSet
    {
        Safe,
        ExternalAccess,
        Unsafe,
    }
}

