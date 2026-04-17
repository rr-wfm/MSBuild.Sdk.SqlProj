using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            var waitForDebugger = false;

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
            if (assemblies.Count == 0)
            {
                Console.Out.WriteLine("No assemblies supplied; nothing to do.");
                return 0;
            }

            try
            {
                AddAssembliesToDacpac(inputPath, defaultPermissionSet, assemblies);
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
            List<string> assemblies)
        {
            Console.Out.WriteLine($"Adding {assemblies.Count} assembly reference(s) to '{inputPath}'");

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
                new ModelLoadOptions(DacSchemaModelStorageType.Memory, loadAsScriptBackedModel: true)))
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

                //File.Delete(inputPath);
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
            Console.Error.WriteLine("Usage: DacpacToolFramework.exe add-assemblies -i <input.dacpac> [-ps Safe|ExternalAccess|Unsafe] [-asm <dll> ...] [--debug]");
        }
    }

    internal enum AssemblyPermissionSet
    {
        Safe,
        ExternalAccess,
        Unsafe,
    }
}
