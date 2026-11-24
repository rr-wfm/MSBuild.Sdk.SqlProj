#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class VersionCheckerTests
    {
        private readonly TestConsole _console = new();
        private readonly DirectoryInfo _cacheDirectory = Directory.CreateTempSubdirectory("sqlproj-version-");

        private string CacheFile => Path.Join(_cacheDirectory.FullName, "release.txt");

        [TestCleanup]
        public void DeleteCacheDirectory()
        {
            File.Delete(CacheFile);
            _cacheDirectory.Delete();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task FetchesAndCachesReleaseWhenCacheIsMissingOrExpired(bool expired)
        {
            if (expired)
            {
                File.WriteAllText(CacheFile, "1.0.0");
                File.SetLastWriteTimeUtc(CacheFile, DateTime.UtcNow.AddDays(-8));
            }

            var lookups = 0;
            var checker = new VersionChecker(_console, new VersionProvider("1.17.0+4c0175a82e"), _ =>
            {
                lookups++;
                return Task.FromResult<string?>("v9.8.7");
            }, CacheFile);

            await checker.CheckForPackageUpdateAsync();

            lookups.ShouldBe(1);
            File.ReadAllText(CacheFile).ShouldBe("9.8.7");
            AssertUpdateWarning();
        }

        [TestMethod]
        public async Task UsesFreshCacheWithoutFetchingRelease()
        {
            File.WriteAllText(CacheFile, "9.8.7");
            var checker = new VersionChecker(_console, new VersionProvider("1.17.0"),
                _ => throw new InvalidOperationException("A fresh cache should bypass the release lookup."), CacheFile);

            await checker.CheckForPackageUpdateAsync();

            AssertUpdateWarning();
        }

        [TestMethod]
        public async Task ReplacesFreshInvalidCacheWithNormalizedReleaseVersion()
        {
            File.WriteAllText(CacheFile, "not-a-version");
            File.SetLastWriteTimeUtc(CacheFile, DateTime.UtcNow);
            var lookups = 0;
            var checker = new VersionChecker(_console, new VersionProvider("1.17.0"), _ =>
            {
                lookups++;
                return Task.FromResult<string?>("v9.8.7.0");
            }, CacheFile);

            await checker.CheckForPackageUpdateAsync();

            lookups.ShouldBe(1);
            File.ReadAllText(CacheFile).ShouldBe("9.8.7");
            _console.Lines.Count.ShouldBe(1);
            _console.Lines[0].ShouldStartWith("DacpacTool warning SQLPROJ0002:");
        }

        [TestMethod]
        [DataRow("9.8.7+build")]
        [DataRow("9.8.8")]
        public async Task DoesNotWarnWhenInstalledVersionIsCurrentOrNewer(string installedVersion)
        {
            var checker = new VersionChecker(_console, new VersionProvider(installedVersion),
                _ => Task.FromResult<string?>("v9.8.7"), CacheFile);

            await checker.CheckForPackageUpdateAsync();

            File.ReadAllText(CacheFile).ShouldBe("9.8.7");
            _console.Lines.ShouldBeEmpty();
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("not-a-version")]
        public async Task DoesNotCacheOrWarnWhenReleaseTagIsUnusable(string? releaseTag)
        {
            var checker = new VersionChecker(_console, new VersionProvider("1.17.0"),
                _ => Task.FromResult(releaseTag), CacheFile);

            await checker.CheckForPackageUpdateAsync();

            File.Exists(CacheFile).ShouldBeFalse();
            _console.Lines.ShouldBeEmpty();
        }

        [TestMethod]
        public async Task DoesNotCacheOrWarnWhenReleaseLookupFails()
        {
            var checker = new VersionChecker(_console, new VersionProvider("1.17.0"),
                _ => Task.FromException<string?>(new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests)), CacheFile);

            await checker.CheckForPackageUpdateAsync();

            File.Exists(CacheFile).ShouldBeFalse();
            _console.Lines.ShouldBeEmpty();
        }

        private void AssertUpdateWarning()
        {
            _console.Lines.Count.ShouldBe(1);
            _console.Lines[0].ShouldStartWith("DacpacTool warning SQLPROJ0002:");
            _console.Lines[0].ShouldContain("<Project Sdk=\"MSBuild.Sdk.SqlProj/9.8.7\">");
        }
    }
}
