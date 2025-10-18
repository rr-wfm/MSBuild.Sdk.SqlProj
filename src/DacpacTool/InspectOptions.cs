using System.IO;

namespace MSBuild.Sdk.SqlProj.DacpacTool;

public class InspectOptions : BaseOptions
{
    public FileInfo PreDeploy { get; set; }
    public FileInfo PostDeploy { get; set; }
}
