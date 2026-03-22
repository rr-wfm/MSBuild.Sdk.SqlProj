using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public abstract class BaseOptions
    {
        [CliOption(Description = "Emit verbose build progress output", Name = "--verbose")]
        public bool Verbose { get; set; }

#if DEBUG
        [CliOption(Description = "Waits for a debugger to attach", Name = "--debug")]
        public bool Debug { get; set; }
#endif
    }
}
