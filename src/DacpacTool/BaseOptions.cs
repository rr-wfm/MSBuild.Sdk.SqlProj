using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public abstract class BaseOptions
    {
#if DEBUG
        [CliOption(Description = "Waits for a debugger to attach")]
#endif
        public bool Debug { get; set; }
    }
}
