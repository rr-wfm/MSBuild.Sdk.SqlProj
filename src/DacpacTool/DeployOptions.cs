using System.IO;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    internal class DeployOptions : BaseOptions
    {
        public FileInfo Input { get; set; }
        public string TargetServerName { get; set; }
        public int? TargetPort { get; set; }
        public string TargetDatabaseName { get; set; }
        public string TargetUser { get; set; }
        public string TargetPassword { get; set; }
        public string[] Property { get; set; }
        public string[] SqlCmdVar { get; set; }
        public bool RunScriptsFromReferences { get; set; }
        public bool Encrypt { get; set; }
    }
}
