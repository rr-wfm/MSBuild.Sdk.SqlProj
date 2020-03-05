using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.BuildDacpac
{
    class Program
    {
        static void Main(string[] args)
        {
            var tSqlModelOptions = new TSqlModelOptions
            {
                AnsiNullsOn = true,
                Collation = "SQL_Latin1_General_CP1_CI_AI",
                CompatibilityLevel = 110,
                QuotedIdentifierOn = true,
            };

            var outputFilename = "output.dacpac";
            var outputFilenameArgument = args.FirstOrDefault(a => a.StartsWith("-o:"));
            if (outputFilenameArgument != null)
            {
                outputFilename = outputFilenameArgument.Substring(3);
            }

            using (var tSqlModel = new TSqlModel(SqlServerVersion.Sql110, tSqlModelOptions))
            {
                foreach (var item in args)
                {
                    if (item.StartsWith("-r:"))
                    {
                        var reference = item.Substring(3);
                        System.Console.WriteLine($"Adding reference to {reference}...");
                        tSqlModel.AddReference(reference);
                    }
                    else if (!item.StartsWith("-o:"))
                    {
                        System.Console.WriteLine($"Adding {item} to model...");
                        tSqlModel.AddObjects(File.ReadAllText(item));
                    }
                }

                if (File.Exists(outputFilename))
                {
                    File.Delete(outputFilename);
                }

                System.Console.WriteLine($"Writing model to {outputFilename}");
                DacPackageExtensions.BuildPackage(outputFilename, tSqlModel, new PackageMetadata { Name = outputFilename }, new PackageOptions { });
            }
        }
    }
}
