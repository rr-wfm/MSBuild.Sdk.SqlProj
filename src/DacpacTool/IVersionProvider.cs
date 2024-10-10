using NuGet.Versioning;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public interface IVersionProvider
    {
        NuGetVersion CurrentPackageVersion();
    }
}
