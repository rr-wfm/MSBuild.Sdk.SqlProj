#nullable enable

using System.Net.Http.Json;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    internal static class GitHubReleaseTagSource
    {
#pragma warning disable CA1812 // Instantiated by JSON deserialization
        private sealed record Release(string? tag_name);
#pragma warning restore CA1812

        internal static async Task<string?> GetLatestTagAsync(CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient
            {
                DefaultRequestHeaders = { { "User-Agent", "MSBuild.Sdk.SqlProj" } },
            };

            var release = await httpClient.GetFromJsonAsync<Release>(
                "https://api.github.com/repos/rr-wfm/MSBuild.Sdk.SqlProj/releases/latest", cancellationToken).ConfigureAwait(false);
            return release?.tag_name;
        }
    }
}

#nullable restore
