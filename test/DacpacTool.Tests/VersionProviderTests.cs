using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Versioning;
using Shouldly;
using DacpacVersionProvider = MSBuild.Sdk.SqlProj.DacpacTool.VersionProvider;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class VersionProviderTests
    {
        [TestMethod]
        public void CurrentPackageVersion_ReturnsAssemblyInformationalVersion()
        {
            var expected = typeof(DacpacVersionProvider)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion;

            var versionProvider = new DacpacVersionProvider();

            NuGetVersion.Parse(expected).ShouldBe(versionProvider.CurrentPackageVersion());
        }
    }
}
