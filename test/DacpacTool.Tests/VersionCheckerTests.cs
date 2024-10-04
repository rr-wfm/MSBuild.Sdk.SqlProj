﻿using System.Globalization;
using System.IO;
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class VersionCheckerTests
    {
        private readonly IConsole _console = new TestConsole();

        [TestMethod]
        public async Task RunsVersionCheck()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var versionChecker = new VersionChecker(_console, new VersionProvider("1.17.0+4c0175a82e"));

            var cacheFile = Path.Join(Path.GetTempPath(), "MSBuild.Sdk.SqlProj.tag-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
            }

            // Act
            await versionChecker.CheckForPackageUpdateAsync();

            // Assert
            testConsole.Lines.Count.ShouldBe(1);
            testConsole.Lines[0].ShouldStartWith($"DacpacTool warning SQLPROJ0002: Your are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: ");

            // Arrange
            testConsole.Lines.Clear();

            // Act
            await versionChecker.CheckForPackageUpdateAsync();

            testConsole.Lines.Count.ShouldBe(1);
            testConsole.Lines[0].ShouldStartWith($"DacpacTool warning SQLPROJ0002: Your are not using the latest version of this SDK, please update to get the latest bug fixes, features and support. Modify your project file: ");
            File.Exists(cacheFile).ShouldBeTrue();
        }

        [TestMethod]
        public async Task RunsVersionCheckAndNoLog()
        {
            // Arrange
            var testConsole = (TestConsole)_console;
            testConsole.Lines.Clear();
            var versionChecker = new VersionChecker(_console, new VersionProvider("9999999.9999999.0+4c0175a82e"));

            // Act
            await versionChecker.CheckForPackageUpdateAsync();

            // Assert
            testConsole.Lines.Count.ShouldBe(0);
        }
    }
}
