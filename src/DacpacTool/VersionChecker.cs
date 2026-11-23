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
            var fileVersion = FileVersionInfo.GetVersionInfo(
                typeof(Microsoft.SqlServer.Dac.DacPackage).Assembly.Location).FileVersion;
            Console.WriteLine($"Using DacFX version: {fileVersion}");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                var latestVersion = await ReadFreshCacheAsync(cts.Token).ConfigureAwait(false);

                if (latestVersion is null)
                {
                    var releaseTag = await _getLatestReleaseTagAsync(cts.Token).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(releaseTag)
                        || !NuGetVersion.TryParse(releaseTag.TrimStart('v'), out var fetchedVersion)
                        || fetchedVersion is null)
                    {
                        return;
                    }

                    latestVersion = fetchedVersion;
                    await WriteCacheAtomicallyAsync(
                        latestVersion.ToNormalizedString(),
                        cts.Token).ConfigureAwait(false);
                }

                if (latestVersion > _versionProvider.CurrentPackageVersion())
                {
                    _console.WriteLine($"DacpacTool warning SQLPROJ0002: You are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: '<Project Sdk=\"MSBuild.Sdk.SqlProj/{latestVersion}\">'. You can ignore this warning by adding '<NoWarn>$(NoWarn);SQLPROJ0002</NoWarn>' to your project file.");
                }

                async Task<NuGetVersion?> ReadFreshCacheAsync(CancellationToken cancellationToken)
                {
                    if (!File.Exists(_cacheFile)
                        || File.GetLastWriteTimeUtc(_cacheFile) <= DateTime.UtcNow.AddDays(-7))
                    {
                        return null;
                    }

                    var content = await File.ReadAllTextAsync(_cacheFile, cancellationToken)
                        .ConfigureAwait(false);

                    return NuGetVersion.TryParse(content, out var version) ? version : null;
                }

                async Task WriteCacheAtomicallyAsync(
                    string content,
                    CancellationToken cancellationToken)
                {
                    var fullPath = Path.GetFullPath(_cacheFile);
                    var directory = Path.GetDirectoryName(fullPath)!;
                    Directory.CreateDirectory(directory);

                    var temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
                    try
                    {
                        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken)
                            .ConfigureAwait(false);
                        File.Move(temporaryPath, fullPath, overwrite: true);
                    }
                    finally
                    {
                        File.Delete(temporaryPath);
                    }
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
