using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{

    public class VersionChecker
    {
        private readonly IConsole _console;
        private readonly IVersionProvider _versionProvider;

        public VersionChecker(IConsole console, IVersionProvider versionProvider) 
        {
            _versionProvider = versionProvider;
            _console = console;
        }

        public async Task CheckForPackageUpdateAsync()
        {
            try
            {
                var logger = new NullLogger();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                using var cache = new SourceCacheContext();
                var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
                var resource = await repository.GetResourceAsync<PackageMetadataResource>().ConfigureAwait(false);

                var packages = await resource.GetMetadataAsync(
                    "MSBuild.Sdk.SqlProj",
                    includePrerelease: false,
                    includeUnlisted: false,
                    cache,
                    logger,
                    cts.Token).ConfigureAwait(false);

                var latestVersion = packages.Select(v => v.Identity.Version).MaxBy(v => v);
                if (latestVersion > _versionProvider.CurrentPackageVersion())
                {
                    _console.WriteLine($"DacpacTool warning SQLPROJ0002: Your are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: '<Project Sdk=\"MSBuild.Sdk.SqlProj/{latestVersion}\">')");
                }
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                // Ignore
            }
#pragma warning restore CA1031
        }
    }
}
