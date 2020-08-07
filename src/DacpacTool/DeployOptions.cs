using System.IO;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class DeployOptions
    {
        public FileInfo Input { get; set; }
        public string TargetServer { get; set; }
        public string TargetDatabaseName { get; set; }
        public string Username { get; set; }
        public string[] Property { get; set; }
    }
}
