using System;
using System.IO;
using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [CliCommand(Name = "create-publish-profile", Description = "Create a credential-free .publish.xml file without connecting to a database")]
    internal sealed class CreatePublishProfileOptions
    {
        [CliOption(Name = "--output", Alias = "-o", Required = true, Description = "Output .publish.xml path (parent directory must exist)")]
        public FileInfo Output { get; set; }

        [CliOption(Name = "--targetServerName", Alias = "-tsn", Required = true, Description = "Target server, optionally including instance or port")]
        public string TargetServerName { get; set; }

        [CliOption(Name = "--targetPort", Required = false, Description = "Target port, overriding any port in the server name")]
        public int? TargetPort { get; set; }

        [CliOption(Name = "--targetDatabaseName", Alias = "-tdn", Required = true, Description = "Target database name")]
        public string TargetDatabaseName { get; set; }

        [CliOption(Name = "--force", Required = false, Description = "Explicitly replace an existing profile")]
        public bool Force { get; set; }

        public int Run()
        {
            try
            {
                PublishProfileWriter.Create(this);
                Console.WriteLine($"Created publish profile '{Output.FullName}'. No database was contacted.");
                return 0;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"ERROR: Could not create publish profile: {ex.Message}");
                return 1;
            }
        }
    }
}
