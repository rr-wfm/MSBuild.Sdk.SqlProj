using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class PackageBuilderTest
    {
        [TestMethod]
        public void UsingVersion()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();

            // Act
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Assert
            packageBuilder.Model.Version.ShouldBe(SqlServerVersion.Sql150);
        }

        [TestMethod]
        public void AddInputFile_FileDoesNotExist()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act
            Should.Throw<ArgumentException>(() => packageBuilder.AddInputFile(new FileInfo("NonExistentFile.sql")));
        }

        [TestMethod]
        public void AddInputFile_FileExists()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act
            packageBuilder.AddInputFile(new FileInfo("../../../../TestProject/Tables/MyTable.sql"));

            // Assert
            packageBuilder.Model.GetObject(Table.TypeClass, new ObjectIdentifier("dbo", "MyTable"), DacQueryScopes.Default).ShouldNotBeNull();
        }

        [TestMethod]
        public void AddReference_FileDoesNotExist()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act & Assert
            Should.Throw<ArgumentException>(() =>  packageBuilder.AddReference("NonExistentFile.dacpac"));
        }

        [TestMethod]
        public void AddReference_FileExists()
        {
            // Arrange
            var reference = new TestModelBuilder()
                .AddStoredProcedure("MyStoredProcedure", "SELECT 1;")
                .SaveAsPackage();
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act
            packageBuilder.AddReference(reference);

            // Assert
            packageBuilder.Model.GetObject(Procedure.TypeClass, new ObjectIdentifier("dbo", "MyStoredProcedure"), DacQueryScopes.All).ShouldNotBeNull();
            
            // Cleanup
            File.Delete(reference);
        }

        [TestMethod]
        public void AddReference_DifferentDatabase()
        {
            // Arrange
            var reference = new TestModelBuilder()
                .AddStoredProcedure("MyStoredProcedure", "SELECT 1;")
                .SaveAsPackage();
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act
            packageBuilder.AddExternalReference(reference, "SomeOtherDatabase");

            // Assert
            var referencingStoredProcedure = "CREATE PROCEDURE [MyOtherStoredProcedure] AS BEGIN EXEC [SomeOtherDatabase].[dbo].[MyStoredProcedure] END";
            packageBuilder.Model.AddObjects(referencingStoredProcedure);
            packageBuilder.Model.Validate().Any().ShouldBeFalse();

            // Cleanup
            File.Delete(reference);
        }

        [TestMethod]
        public void AddSqlCmdvariable()
        {
            // Arrange
            var first = "DbReader";
            var second = "DbWriter";
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);

            // Act
            packageBuilder.AddSqlCmdVariables(new string[] { first, second });

            // Assert
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);
            var headerParser = new DacpacHeaderParser.HeaderParser(tempFile.FullName);
            
            headerParser.GetCustomData()
                .Where(d => d.Category == "SqlCmdVariables"
                    && d.Type == "SqlCmdVariable")
                .SelectMany(d => d.Items)
                .Where(i => i.Name == "DbReader"
                    && i.Value == string.Empty)
                .ToList().Count.ShouldBe(1);

            headerParser.GetCustomData()
                .Where(d => d.Category == "SqlCmdVariables"
                    && d.Type == "SqlCmdVariable")
                .SelectMany(d => d.Items)
                .Where(i => i.Name == "DbWriter"
                    && i.Value == string.Empty)
                .ToList().Count.ShouldBe(1);

            // Cleanup
            tempFile.Delete();
        }

        [TestMethod]
        public void AddPreDeployment_FilesExist()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();
            var packageOptions = new PackageOptions() { RefactorLogPath = "../../../../TestProjectWithPrePost/RefactorLog/TestProjectWithPrePost.refactorlog" };

            // Act
            packageBuilder.SaveToDisk(tempFile, packageOptions);

            packageBuilder.AddPreDeploymentScript(
                new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script.PreDeployment.sql"),
                tempFile);

            packageBuilder.AddPostDeploymentScript(
                new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script.PostDeployment.sql"),
                tempFile);

            // Assert
            var package = Package.Open(tempFile.FullName);
            var prePart = package.GetPart(new Uri("/predeploy.sql", UriKind.Relative));            
            var postPart = package.GetPart(new Uri("/postdeploy.sql", UriKind.Relative));
            var refactorPart = package.GetPart(new Uri("/refactor.xml", UriKind.Relative));

            prePart.ShouldNotBeNull();
            prePart.ContentType.ShouldBe("text/plain");
            prePart.GetStream().ShouldNotBeNull();

            postPart.ShouldNotBeNull();
            postPart.ContentType.ShouldBe("text/plain");
            postPart.GetStream().ShouldNotBeNull();

            refactorPart.ShouldNotBeNull();
            refactorPart.ContentType.ShouldBe("text/xml");
            refactorPart.GetStream().ShouldNotBeNull();

            // Cleanup
            package.Close();
            tempFile.Delete();
        }

        [TestMethod]
        public void AddPreDeployment_NoFilePresent()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);

            // Act
            packageBuilder.AddPreDeploymentScript(
                null,
                tempFile);

            // Assert
            var package = Package.Open(tempFile.FullName);
            
            package.GetParts()
                .Where(p => p.Uri == new Uri("/predeploy.sql", UriKind.Relative))
                .FirstOrDefault()
                .ShouldBeNull();

            package.GetParts()
                .Where(p => p.Uri == new Uri("/postdeploy.sql", UriKind.Relative))
                .FirstOrDefault()
                .ShouldBeNull();

            // Cleanup
            package.Close();
            tempFile.Delete();
        }

        [TestMethod]
        public void AddPostDeployment_NoFilePresent()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);

            // Act
            packageBuilder.AddPostDeploymentScript(
                null,
                tempFile);

            // Assert
            var package = Package.Open(tempFile.FullName);

            package.GetParts()
                .Where(p => p.Uri == new Uri("/postdeploy.sql", UriKind.Relative))
                .FirstOrDefault()
                .ShouldBeNull();

            // Cleanup
            package.Close();
            tempFile.Delete();
        }

        [TestMethod]
        public void AddRefactorLog_NoFilePresent()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();

            // Act
            packageBuilder.SaveToDisk(tempFile, new PackageOptions() { RefactorLogPath = null });

            // Assert
            var package = Package.Open(tempFile.FullName);

            package.GetParts()
                .Where(p => p.Uri == new Uri("/refactor.log", UriKind.Relative))
                .FirstOrDefault()
                .ShouldBeNull();

            // Cleanup
            package.Close();
            tempFile.Delete();
        }

        [TestMethod]
        public void AddPreDeployment_WrongOrder()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);


            // Act & Assert
            Should.Throw<InvalidOperationException>(() => packageBuilder.AddPreDeploymentScript(null, tempFile));
        }

        [TestMethod]
        public void AddPostDeployment_WrongOrder()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);


            // Act & Assert
            Should.Throw<InvalidOperationException>(() => packageBuilder.AddPostDeploymentScript(null, tempFile));
        }

        [TestMethod]
        public void AddPrePostDeployment_PreNotExists()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);

            // Act & Assert
            Should.Throw<ArgumentException>(() => packageBuilder.AddPreDeploymentScript(
                new FileInfo("NonExistingScript.PreDeployment.sql"),
                tempFile));

            // Cleanup
            tempFile.Delete();
        }

        [TestMethod]
        public void AddPostDeployment_PostNotExists()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();
            packageBuilder.SaveToDisk(tempFile);

            // Act & Assert
            Should.Throw<ArgumentException>(() => packageBuilder.AddPostDeploymentScript(
                new FileInfo("NonExistingScript.PostDeployment.sql"),
                tempFile));

            // Cleanup
            tempFile.Delete();
        }

        [TestMethod]
        public void AddRefactorLog_RefactorNotExists()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.ValidateModel();

            // Act & Assert
            Should.Throw<DacServicesException>(() => packageBuilder.SaveToDisk(tempFile, new PackageOptions() { RefactorLogPath = "NonExistingProject.refactorlog" }));

            // Cleanup
            tempFile.Delete();
        }

        [TestMethod]
        [ValidPropertiesTestData]
        public void SetProperty_Valid(PropertyInfo property, string value, object expected)
        {
            // Arrange
            var packageBuilder = new PackageBuilder();

            // Act
            packageBuilder.SetProperty(property.Name, value);

            // Assert
            property.GetValue(packageBuilder.Options).ShouldBe(expected);
        }

        [TestMethod]
        public void SetProperty_UnknownProperty()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();

            // Act & Assert
            Should.Throw<ArgumentException>(() => packageBuilder.SetProperty("MyUnknownProperty", "MyValue"));
        }

        [TestMethod]
        public void SetProperty_InvalidValue()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();

            // Act
            Should.Throw<ArgumentException>(() => packageBuilder.SetProperty("QueryStoreIntervalLength", "MyFancyText"));
        }

        [TestMethod]
        public void SetMetadata()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();

            // Act
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");

            // Assert
            packageBuilder.Metadata.Name.ShouldBe("MyPackage");
            packageBuilder.Metadata.Version.ShouldBe("1.0.0.0");
        }

        [TestMethod]
        public void ValidateModel_ValidModel()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.Model.AddObjects("CREATE PROCEDURE [csp_Test] AS BEGIN SELECT 1 END");

            // Act
            bool result = packageBuilder.ValidateModel();

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void ValidateModel_Warnings()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.Model.AddObjects("CREATE PROCEDURE [csp_Test] AS BEGIN SELECT * FROM [dbo].[MyTable] END");

            // Act
            bool result = packageBuilder.ValidateModel();

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void ValidateModel_Errors()
        {
            // Arrange
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.Model.AddObjects("CREATE PROCEDURE [csp_Test] @p_Parameter [dbo].[CustomType] AS BEGIN SELECT 1 END");

            // Act
            bool result = packageBuilder.ValidateModel();

            // Assert
            result.ShouldBeFalse();
        }

        [TestMethod]
        public void SaveToFile()
        {
            // Arrange
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder();
            packageBuilder.UsingVersion(SqlServerVersion.Sql150);
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.ValidateModel();

            // Act
            packageBuilder.SaveToDisk(tempFile);

            // Assert
            tempFile.Exists.ShouldBeTrue();

            // Cleanup
            tempFile.Delete();
        }

        class ValidPropertiesTestDataAttribute : Attribute, ITestDataSource
        {
            public IEnumerable<object[]> GetData(MethodInfo methodInfo)
            {
                var optionsType = typeof(TSqlModelOptions);
                return new List<object[]> {
                    new object[] { optionsType.GetProperty("QueryStoreIntervalLength"), "1", 1 },
                    new object[] { optionsType.GetProperty("QueryStoreFlushInterval"), "2", 2 },
                    new object[] { optionsType.GetProperty("QueryStoreDesiredState"), "ReadWrite", QueryStoreDesiredState.ReadWrite },
                    new object[] { optionsType.GetProperty("QueryStoreCaptureMode"), "Auto", QueryStoreCaptureMode.Auto },
                    new object[] { optionsType.GetProperty("ParameterizationOption"), "Forced", ParameterizationOption.Forced },
                    new object[] { optionsType.GetProperty("PageVerifyMode"), "TornPageDetection", PageVerifyMode.TornPageDetection },
                    new object[] { optionsType.GetProperty("QueryStoreMaxStorageSize"), "3", 3 },
                    new object[] { optionsType.GetProperty("NumericRoundAbortOn"), "True", true },
                    new object[] { optionsType.GetProperty("NestedTriggersOn"), "False", false },
                    new object[] { optionsType.GetProperty("HonorBrokerPriority"), "True", true },
                    new object[] { optionsType.GetProperty("FullTextEnabled"), "False", false },
                    new object[] { optionsType.GetProperty("FileStreamDirectoryName"), "Test", "Test" },
                    new object[] { optionsType.GetProperty("DbScopedConfigQueryOptimizerHotfixesSecondary"), "True", true },
                    new object[] { optionsType.GetProperty("DbScopedConfigQueryOptimizerHotfixes"), "False", false },
                    new object[] { optionsType.GetProperty("NonTransactedFileStreamAccess"), "ReadOnly", NonTransactedFileStreamAccess.ReadOnly },
                    new object[] { optionsType.GetProperty("DbScopedConfigParameterSniffingSecondary"), "True", true },
                    new object[] { optionsType.GetProperty("QueryStoreMaxPlansPerQuery"), "10", 10 },
                    new object[] { optionsType.GetProperty("QuotedIdentifierOn"), "False", false },
                    new object[] { optionsType.GetProperty("VardecimalStorageFormatOn"), "True", true },
                    new object[] { optionsType.GetProperty("TwoDigitYearCutoff"), "10", 10 },
                    new object[] { optionsType.GetProperty("Trustworthy"), "False", false },
                    new object[] { optionsType.GetProperty("TransformNoiseWords"), "True", true },
                    new object[] { optionsType.GetProperty("TornPageProtectionOn"), "False", false },
                    new object[] { optionsType.GetProperty("TargetRecoveryTimeUnit"), "Hours", TimeUnit.Hours },
                    new object[] { optionsType.GetProperty("QueryStoreStaleQueryThreshold"), "42", 42 },
                    new object[] { optionsType.GetProperty("TargetRecoveryTimePeriod"), "11", 11 },
                    new object[] { optionsType.GetProperty("ServiceBrokerOption"), "ErrorBrokerConversations", ServiceBrokerOption.ErrorBrokerConversations },
                    new object[] { optionsType.GetProperty("RecursiveTriggersOn"), "True", true } ,
                    new object[] { optionsType.GetProperty("DelayedDurabilityMode"), "Forced", DelayedDurabilityMode.Forced },
                    new object[] { optionsType.GetProperty("RecoveryMode"), "BulkLogged", RecoveryMode.BulkLogged },
                    new object[] { optionsType.GetProperty("ReadOnly"), "False", false },
                    new object[] { optionsType.GetProperty("SupplementalLoggingOn"), "True", true },
                    new object[] { optionsType.GetProperty("DbScopedConfigParameterSniffing"), "False", false },
                    new object[] { optionsType.GetProperty("DbScopedConfigMaxDOPSecondary"), "12", 12 },
                    new object[] { optionsType.GetProperty("DbScopedConfigMaxDOP"), "13", 13 },
                    new object[] { optionsType.GetProperty("AutoShrink"), "True", true },
                    new object[] { optionsType.GetProperty("AutoCreateStatisticsIncremental"), "False", false },
                    new object[] { optionsType.GetProperty("AutoCreateStatistics"), "True", true },
                    new object[] { optionsType.GetProperty("AutoClose"), "False", false },
                    new object[] { optionsType.GetProperty("ArithAbortOn"), "True", true },
                    new object[] { optionsType.GetProperty("AnsiWarningsOn"), "False", false },
                    new object[] { optionsType.GetProperty("AutoUpdateStatistics"), "True", true },
                    new object[] { optionsType.GetProperty("AnsiPaddingOn"), "False", false },
                    new object[] { optionsType.GetProperty("AnsiNullDefaultOn"), "True", true },
                    new object[] { optionsType.GetProperty("MemoryOptimizedElevateToSnapshot"), "False", false },
                    new object[] { optionsType.GetProperty("TransactionIsolationReadCommittedSnapshot"), "True", true },
                    new object[] { optionsType.GetProperty("AllowSnapshotIsolation"), "False", false },
                    new object[] { optionsType.GetProperty("Collation"), "Test", "Test" },
                    new object[] { optionsType.GetProperty("AnsiNullsOn"), "True", true },
                    new object[] { optionsType.GetProperty("AutoUpdateStatisticsAsync"), "False", false },
                    new object[] { optionsType.GetProperty("CatalogCollation"), "Latin1_General_100_CI_AS_KS_WS_SC", CatalogCollation.Latin1_General_100_CI_AS_KS_WS_SC },
                    new object[] { optionsType.GetProperty("ChangeTrackingAutoCleanup"), "True", true },
                    new object[] { optionsType.GetProperty("DbScopedConfigLegacyCardinalityEstimationSecondary"), "False", false },
                    new object[] { optionsType.GetProperty("DbScopedConfigLegacyCardinalityEstimation"), "True", true },
                    new object[] { optionsType.GetProperty("DBChainingOn"), "False", false },
                    new object[] { optionsType.GetProperty("DefaultLanguage"), "Test", "Test" },
                    new object[] { optionsType.GetProperty("DefaultFullTextLanguage"), "Test", "Test" },
                    new object[] { optionsType.GetProperty("DateCorrelationOptimizationOn"), "True", true },
                    new object[] { optionsType.GetProperty("DatabaseStateOffline"), "False", false },
                    new object[] { optionsType.GetProperty("CursorDefaultGlobalScope"), "True", true },
                    new object[] { optionsType.GetProperty("CursorCloseOnCommit"), "False", false },
                    new object[] { optionsType.GetProperty("Containment"), "Partial", Containment.Partial },
                    new object[] { optionsType.GetProperty("ConcatNullYieldsNull"), "True", true },
                    new object[] { optionsType.GetProperty("CompatibilityLevel"), "9", 9 },
                    new object[] { optionsType.GetProperty("ChangeTrackingRetentionUnit"), "Days", TimeUnit.Days },
                    new object[] { optionsType.GetProperty("ChangeTrackingRetentionPeriod"), "8", 8 },
                    new object[] { optionsType.GetProperty("ChangeTrackingEnabled"), "False", false },
                    new object[] { optionsType.GetProperty("UserAccessOption"), "Restricted", UserAccessOption.Restricted },
                    new object[] { optionsType.GetProperty("WithEncryption"), "True", true },
                };
            }

            public string GetDisplayName(MethodInfo methodInfo, object[] data)
            {
                if (data != null)
                {
                    var result = $"{methodInfo.Name} ({string.Join(",", data)})";
                    return result;
                }

                return null;
            }
        }
    }
}
