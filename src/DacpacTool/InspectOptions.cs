using System.IO;
using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    [CliCommand(Description = "Collect includes from pre- and post-deployment scripts", 
                Name = "collect-includes")]
    public class InspectOptions : BaseOptions
    {
        [CliOption(Description = "Filename of optional pre-deployment script", Name = "--predeploy", Required = false)]
        public FileInfo PreDeploy { get; set; }
        
        [CliOption(Description = "Filename of optional post-deployment script", Name = "--postdeploy", Required = false)]
        public FileInfo PostDeploy { get; set; }
        
        public int Run()
        {
            return Program.InspectIncludes(this);
        }
    }
}
