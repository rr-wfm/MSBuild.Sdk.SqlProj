#nullable enable

using System.Diagnostics;
using NuGet.Versioning;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class VersionChecker
    {
        private readonly IConsole _console;
        private readonly IVersionProvider _versionProvider;
        private readonly Func<CancellationToken, Task<string?>> _getLatestReleaseTagAsync;
        private readonly string _cacheFile;

        public VersionChecker(IConsole console, IVersionProvider versionProvider)
            : this(console, versionProvider, GitHubReleaseTagSource.GetLatestTagAsync,
                Path.Join(Path.GetTempPath(), "MSBuild.Sdk.SqlProj.tag.txt"))
        {
        }

        internal VersionChecker(IConsole console, IVersionProvider versionProvider,
            Func<CancellationToken, Task<string?>> getLatestReleaseTagAsync, string cacheFile)
        {
            _console = console;
            _versionProvider = versionProvider;
            _getLatestReleaseTagAsync = getLatestReleaseTagAsync;
            _cacheFile = cacheFile;
        }

        public async Task CheckForPackageUpdateAsync()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(typeof(Microsoft.SqlServer.Dac.DacPackage).Assembly.Location).FileVersion;
            _console.WriteLine($"Using DacFX version: {fileVersion}");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                NuGetVersion latestVersion;

                if (File.Exists(_cacheFile) && File.GetLastWriteTimeUtc(_cacheFile) > DateTime.UtcNow.AddDays(-7))
                {
                    var cache = await File.ReadAllTextAsync(_cacheFile, cts.Token).ConfigureAwait(false);
                    latestVersion = NuGetVersion.Parse(cache);
                }
                else
                {
                    var releaseTag = await _getLatestReleaseTagAsync(cts.Token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(releaseTag))
                    {
                        return;
                    }

                    latestVersion = NuGetVersion.Parse(releaseTag.TrimStart('v'));
                    await File.WriteAllTextAsync(_cacheFile, latestVersion.ToNormalizedString(), cts.Token).ConfigureAwait(false);
                }

                if (latestVersion > _versionProvider.CurrentPackageVersion())
                {
                    _console.WriteLine($"DacpacTool warning SQLPROJ0002: You are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: '<Project Sdk=\"MSBuild.Sdk.SqlProj/{latestVersion}\">'. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0002</NoWarn>' to your project file.");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
            {
                // Ignore
            }
#pragma warning restore CA1031
        }
    }
}
#nullable restore
