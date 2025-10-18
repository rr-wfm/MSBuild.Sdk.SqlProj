using NuGet.Versioning;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests;

public class VersionProvider : IVersionProvider
{
    private readonly string _version;

    public VersionProvider(string version) 
    {
        _version = version;
    }

    public NuGetVersion CurrentPackageVersion()
    {
        return new NuGetVersion(_version);
    }
}
