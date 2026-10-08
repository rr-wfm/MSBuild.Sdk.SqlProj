using Microsoft.Data.SqlClient;
using MSBuild.Sdk.SqlProj.DacpacTool;
using MSBuild.Sdk.SqlProj.TestSupport;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.IntegrationTests
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("SqlServer")]
    public class ReferencedDeploymentScriptTests
    {
        private static SqlServerFixture _fixture = null!;

        [ClassInitialize]
        public static async Task InitializeAsync(TestContext context)
        {
            _fixture = new SqlServerFixture();
            try
            {
                await _fixture.InitializeAsync(context.CancellationToken);
            }
            catch
            {
                await _fixture.DisposeAsync();
                throw;
            }
        }

        [ClassCleanup]
        public static async Task CleanupAsync()
        {
            await _fixture.DisposeAsync();
        }

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public void DeployDacpac_OnlyRunsReferencePostDeploymentAfterSuccess(bool failDeployment, bool runScriptsFromReferences)
        {
            var connection = new SqlConnectionStringBuilder(_fixture.SqlServer.GetConnectionString())
            {
                InitialCatalog = "master",
            };
            var databaseName = $"SqlProjDeploymentTest_{Guid.NewGuid():N}";
            var databaseConnection = new SqlConnectionStringBuilder(connection.ConnectionString)
            {
                InitialCatalog = databaseName,
            };
            var directory = Directory.CreateTempSubdirectory("SqlProjDeploymentTest_");
            var databaseCreated = false;
            using var writer = new StringWriter();
            var originalOut = Console.Out;

            try
            {
                ExecuteScalar(connection.ConnectionString, $"CREATE DATABASE [{databaseName}];");
                databaseCreated = true;
                ExecuteScalar(databaseConnection.ConnectionString, "CREATE TABLE dbo.DeploymentMarker (Id int NOT NULL);");

                var postDeploy = new FileInfo(Path.Combine(directory.FullName, "PostDeploy.sql"));
                File.WriteAllText(postDeploy.FullName, "INSERT INTO dbo.DeploymentMarker (Id) VALUES (42);\nPRINT N'REFERENCE_POSTDEPLOY_RAN';\nGO\n");
                var reference = BuildPackage(directory, "Reference", "CREATE TABLE dbo.DeploymentMarker (Id int NOT NULL);", postDeploy: postDeploy);

                FileInfo? preDeploy = null;
                if (failDeployment)
                {
                    preDeploy = new FileInfo(Path.Combine(directory.FullName, "PreDeploy.sql"));
                    File.WriteAllText(preDeploy.FullName, "THROW 51000, 'INTENTIONAL_DEPLOYMENT_FAILURE', 1;\nGO\n");
                }

                var package = BuildPackage(directory, "Main", "CREATE TABLE dbo.MainChange (Id int NOT NULL);", reference, preDeploy);
                var options = new DeployOptions
                {
                    Input = package,
                    TargetServerName = connection.DataSource,
                    TargetDatabaseName = databaseName,
                    TargetUser = connection.UserID,
                    TargetPassword = connection.Password,
                    Property = ["IncludeCompositeObjects=True"],
                    RunScriptsFromReferences = runScriptsFromReferences,
                };

                Console.SetOut(writer);
                var result = Program.DeployDacpac(options);
                Console.SetOut(originalOut);
                var output = writer.ToString();
                Console.WriteLine(output);

                if (failDeployment)
                {
                    output.ShouldContain("INTENTIONAL_DEPLOYMENT_FAILURE");
                    output.ShouldNotContain("Running post-deployment script for referenced package");
                }

                var expectedMarkerCount = !failDeployment && runScriptsFromReferences ? 1 : 0;
                ExecuteScalar(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM dbo.DeploymentMarker;")
                    .ShouldBe(expectedMarkerCount, output);
                ExecuteScalar(databaseConnection.ConnectionString, "SELECT COUNT(*) FROM sys.tables WHERE name = N'MainChange' AND schema_id = SCHEMA_ID(N'dbo');")
                    .ShouldBe(failDeployment ? 0 : 1, output);
                result.ShouldBe(failDeployment ? 1 : 0, output);
            }
            finally
            {
                Console.SetOut(originalOut);
                try
                {
                    if (databaseCreated)
                    {
                        ExecuteScalar(connection.ConnectionString,
                            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
                    }
                }
                finally
                {
                    directory.Delete(true);
                }
            }
        }

        private static FileInfo BuildPackage(DirectoryInfo directory, string name, string sql, FileInfo? reference = null, FileInfo? preDeploy = null, FileInfo? postDeploy = null)
        {
            using var builder = new PackageBuilder(new TestConsole());
            builder.UsingVersion(_fixture.TargetPlatform);
            builder.SetMetadata(name, "1.0.0");
            if (reference != null)
            {
                builder.AddReference(reference.FullName);
            }

            builder.Model.AddObjects(sql);
            builder.ValidateModel().ShouldBeTrue();
            var package = new FileInfo(Path.Combine(directory.FullName, $"{name}.dacpac"));
            builder.SaveToDisk(package);
            builder.AddOutputArtifacts(null, preDeploy, postDeploy, null, package);
            package.Refresh();
            return package;
        }

        private static object? ExecuteScalar(string connectionString, string sql)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }
    }
}
