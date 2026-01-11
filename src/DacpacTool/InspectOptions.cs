using System.IO;
using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [CliCommand(Description = "Collect includes from pre- and post-deployment scripts")]
    public class InspectOptions : BaseOptions
    {
        [CliOption(Description = "Filename of optional pre-deployment script")]
        public FileInfo PreDeploy { get; set; } = null;

        [CliOption(Description = "Filename of optional post-deployment script")]
        public FileInfo PostDeploy { get; set; } = null;
    }
}
