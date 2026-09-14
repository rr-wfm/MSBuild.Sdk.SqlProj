using System;
using System.IO;
using System.IO.Packaging;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class DacOriginModifierTests
    {
        private static FileInfo BuildPackage()
        {
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder(new TestConsole());
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql160);
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);
            return tempFile;
        }

        private static string ReadOriginXml(FileInfo dacpac)
        {
            using var package = Package.Open(dacpac.FullName, FileMode.Open, FileAccess.Read);
            var part = package.GetPart(new Uri("/Origin.xml", UriKind.Relative));
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        [TestMethod]
        public void SetProjectGuid_WritesProjectGuidToOriginXml()
        {
            // Arrange
            var tempFile = BuildPackage();
            var projectGuid = Guid.NewGuid();

            // Act
            var modifier = new DacOriginModifier(new TestConsole());
            using (var package = Package.Open(tempFile.FullName, FileMode.Open, FileAccess.ReadWrite))
            {
                modifier.SetProjectGuid(package, projectGuid);
                package.Close();
            }

            // Assert
            var originXml = ReadOriginXml(tempFile);
            originXml.ShouldContain(projectGuid.ToString("D"), Case.Insensitive);

            // Cleanup
            tempFile.Delete();
        }

        [TestMethod]
        public void SetProjectGuid_NullPackage_Throws()
        {
            // Arrange
            var modifier = new DacOriginModifier(new TestConsole());

            // Act & Assert
            Should.Throw<ArgumentNullException>(() =>
                modifier.SetProjectGuid(null, Guid.NewGuid()));
        }

        [TestMethod]
        public void BuildPackage_WithoutSetProjectGuid_OriginHasNoProjectGuid()
        {
            // Arrange
            var tempFile = BuildPackage();

            // Act
            var originXml = ReadOriginXml(tempFile);

            // Assert
            originXml.ShouldNotContain("<ProjectGuid");

            // Cleanup
            tempFile.Delete();
        }
    }
}
