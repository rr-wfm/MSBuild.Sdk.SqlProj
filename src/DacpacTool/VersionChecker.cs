using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http.Json;
using NuGet.Versioning;
using System.IO;
using System.Globalization;
using System.Net.Http;

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
                var timeout = TimeSpan.FromSeconds(2);

                using var cts = new CancellationTokenSource(timeout);

                var cacheFile = Path.Join(Path.GetTempPath(), "MSBuild.Sdk.SqlProj.tag-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

                NuGetVersion latestVersion = null;

                if (File.Exists(cacheFile))
                {
                    var cache = await File.ReadAllTextAsync(cacheFile, cts.Token).ConfigureAwait(false);
                    latestVersion = NuGetVersion.Parse(cache);
                }
                else
                {
                    using var httpClient = new HttpClient
                    {
                        DefaultRequestHeaders = { { "User-Agent", "MSBuild.Sdk.SqlProj" } },
                        Timeout = timeout,
                    };

                    var response = await httpClient.GetFromJsonAsync<Release>("https://api.github.com/repos/rr-wfm/MSBuild.Sdk.SqlProj/releases/latest").ConfigureAwait(false);
                    if (response is null || response.tag_name is null || response.draft || response.prerelease)
                    {
                        return;
                    }

                    latestVersion = NuGetVersion.Parse(response.tag_name.TrimStart('v'));

                    await File.WriteAllTextAsync(cacheFile, latestVersion.ToNormalizedString(), cts.Token).ConfigureAwait(false);
                }

                if (latestVersion is null)
                {
                    return;
                }

                if (latestVersion > _versionProvider.CurrentPackageVersion())
                {
                    _console.WriteLine($"DacpacTool warning SQLPROJ0002: You are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: '<Project Sdk=\"MSBuild.Sdk.SqlProj/{latestVersion}\">')");
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


    internal class Release
    {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
    }
}
