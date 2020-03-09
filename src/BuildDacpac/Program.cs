using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dac;
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

            rootCommand.Description = "BuildDacpac";
            rootCommand.Handler = CommandHandler.Create<string, string, FileInfo, SqlServerVersion, FileInfo[], FileInfo[], string[]>(BuildDacpac);

            return await rootCommand.InvokeAsync(args);
        }

        private static int BuildDacpac(string name, string version, FileInfo output, SqlServerVersion sqlServerVersion, FileInfo[] input, FileInfo[] reference, string[] property)
        {
            if (input == null)
            {
                System.Console.WriteLine("Expected at least one input file");
                return 1;
            }

            using var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata(name, version);

            if (property != null)
            {
                foreach (var propertyValue in property)
                {
                    string[] keyValuePair = propertyValue.Split('=', 2);
                    packageBuilder.SetProperty(keyValuePair[0], keyValuePair[1]);
                }
            }

            packageBuilder.UsingVersion(sqlServerVersion);

            if (reference != null)
            {
                foreach (var referenceFile in reference)
                {
                    packageBuilder.AddReference(referenceFile);
                }
            }

            foreach (var inputFile in input)
            {
                packageBuilder.AddInputFile(inputFile);
            }

            packageBuilder.SaveToDisk(output);
            return 0;
        }
    }
}
