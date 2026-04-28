using DotMake.CommandLine;

namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

public abstract class BaseOptions
{
#if DEBUG
    [CliOption(Description = "Waits for a debugger to attach", Name = "--debug")]
    public bool Debug { get; set; }
#endif
}
