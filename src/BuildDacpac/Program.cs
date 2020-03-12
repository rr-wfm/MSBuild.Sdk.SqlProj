using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.BuildDacpac
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(new string[] { "--name", "-n" }, "Name of the package"),
                new Option<string>(new string[] { "--version", "-v" }, "Version of the package"),
                new Option<FileInfo>(new string[] { "--output", "-o" }, "Filename of the output package"),
                new Option<SqlServerVersion>(new string[] { "--sqlServerVersion", "-sv" }, () => SqlServerVersion.Sql150, description: "Target version of the model"), 
                new Option<FileInfo[]>(new string[] { "--input", "-i" }, "Input file name(s)"),
                new Option<FileInfo[]>(new string[] { "--reference", "-r" }, "Reference(s) to include"),
                new Option<string[]>(new string[] { "--property", "-p" }, "Properties to be set on the model"),
            };

            rootCommand.Description = "Command line tool for generating a SQL Server Data-Tier Application Framework package (dacpac)";
            rootCommand.Handler = CommandHandler.Create<string, string, FileInfo, SqlServerVersion, FileInfo[], FileInfo[], string[]>(BuildDacpac);

            return await rootCommand.InvokeAsync(args);
        }

        private static int BuildDacpac(string name, string version, FileInfo output, SqlServerVersion sqlServerVersion, FileInfo[] input, FileInfo[] reference, string[] property)
        {
            // Set metadata for the package
            using var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata(name, version);

            // Set properties on the model (if defined)
            if (property != null)
            {
                foreach (var propertyValue in property)
                {
                    string[] keyValuePair = propertyValue.Split('=', 2);
                    packageBuilder.SetProperty(keyValuePair[0], keyValuePair[1]);
                }
            }

            // Build the empty model for te targetted SQL Server version
            packageBuilder.UsingVersion(sqlServerVersion);

            // Add references to the model
            if (reference != null)
            {
                foreach (var referenceFile in reference)
                {
                    packageBuilder.AddReference(referenceFile);
                }
            }

            // Add input files
            if (input != null)
            {
                foreach (var inputFile in input)
                {
                    packageBuilder.AddInputFile(inputFile);
                }
            }

            // Validate the model
            if (!packageBuilder.ValidateModel())
            {
                return 1;
            }

            // Save the package to disk
            packageBuilder.SaveToDisk(output);
            return 0;
        }
    }
}
