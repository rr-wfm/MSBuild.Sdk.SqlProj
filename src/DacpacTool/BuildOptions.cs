using System.IO;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class BuildOptions
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public FileInfo Output { get; set; }
        public SqlServerVersion SqlServerVersion { get; set; }
        public FileInfo[] Input { get; set; }
        public string[] Reference { get; set; }
        public string[] Property { get; set; }
        public string[] SqlCmdVar { get; set; }
        public FileInfo PreDeploy { get; set; }
        public FileInfo PostDeploy { get; set; }
        public FileInfo RefactorLog { get; set; }
        public bool Debug { get; set; }
    }
}
