using System;
using System.IO;
using Microsoft.SqlServer.Dac;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;
using System.IO.Packaging;
using System.Text;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageHelper
    {
        private readonly IConsole _console;

        public PackageHelper(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public void GenerateCreateScript(FileInfo dacpacFile, string databaseName, DacDeployOptions deployOptions)
        {
            ArgumentNullException.ThrowIfNull(dacpacFile);

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("The database name is mandatory.", nameof(databaseName));
            }

            var scriptFileName = $"{databaseName}_Create.sql";
            _console.WriteLine($"Generating create script {scriptFileName}");

            using var package = DacPackage.Load(dacpacFile.FullName);

            if (package == null)
            {
                throw new InvalidOperationException($"Unable to load package {dacpacFile.FullName}");
            }

            if (dacpacFile.DirectoryName == null)
            {
                throw new InvalidOperationException($"Unable to determine directory for package {dacpacFile.FullName}");
            }

            using var file = File.Create(Path.Combine(dacpacFile.DirectoryName, scriptFileName));

            DacServices.GenerateCreateScript(file, package, databaseName, deployOptions);
        }

        public void AddPreDeploymentScript(FileInfo script, FileInfo outputFile)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            AddScript(script, outputFile, "/predeploy.sql");
        }

        public void AddPostDeploymentScript(FileInfo script, FileInfo outputFile)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            AddScript(script, outputFile, "/postdeploy.sql");
        }

        private void AddScript(FileInfo script, FileInfo outputFile, string path)
        {
            if (script == null)
            {
                return;
            }

            if (!script.Exists)
            {
                throw new ArgumentException($"Unable to find script file {script.FullName}", nameof(script));
            }

            using (var package = Package.Open(outputFile.FullName, FileMode.Open, FileAccess.ReadWrite))
            {
                _console.WriteLine($"Adding {script.FullName} to package");
                WritePart(script, package, path);

                package.Close();
            }
        }

        private static void WritePart(FileInfo file, Package package, string path)
        {
            var part = package.CreatePart(new Uri(path, UriKind.Relative), "text/plain");

            using (var stream = part.GetStream())
            {
                using var parser = new ScriptParser(file.FullName, new IncludeVariableResolver());
                var buffer = Encoding.UTF8.GetBytes(parser.GenerateScript());
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
