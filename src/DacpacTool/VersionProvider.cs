using System.Reflection;
using NuGet.Versioning;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class VersionProvider : IVersionProvider
    {
        public NuGetVersion CurrentPackageVersion()
        {
            return new NuGetVersion(
                Assembly
                .GetEntryAssembly()!
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion);
        }
    }
}
