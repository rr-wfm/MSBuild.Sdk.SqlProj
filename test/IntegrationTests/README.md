# Integration tests

This suite tests referenced deployment scripts through DacpacTool and publishing through the SDK's MSBuild targets and SqlPackage against disposable SQL Server containers. Run it when changing deployment behavior or container publishing.

## Scope

`PublishingTests.cs` runs four cases:

| Case | What it verifies |
| --- | --- |
| Built-in publisher | `dotnet publish /t:PublishDatabase` deploys using MSBuild connection and database properties. |
| Local SqlPackage profile | A direct SqlPackage invocation uses `/Profile` to deploy with a fixture-created profile's target settings and leaves the profile unchanged. |
| SqlPackage database override | SqlPackage's `/TargetDatabaseName` overrides the profile's database name and leaves the profile unchanged. |
| Container profile | `PublishContainer` builds a deployment image whose SqlPackage entry point publishes using a read-only mounted profile supplied through `/Profile`. |

The profile cases exercise SqlPackage's existing `.publish.xml` support. The test fixture writes these files directly; it does not use or test the proposed publish-profile item template in [issue 995](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues/995). The SDK's built-in publisher (`dotnet publish /t:PublishDatabase`) uses MSBuild properties and does not consume these profiles. These cases do not introduce profile support to that publisher.

Every publishing case queries the resulting database to verify `dbo.MyTable` has exactly the expected columns: `Column1` (`nvarchar`) and `Column2` (`int`).

The shared [fixture](PublishingFixture.cs) copies the repository's [TestProject](../TestProject/TestProject.csproj) into a temporary directory, uses the SDK and DacpacTool from the checkout, and writes a minimal SqlPackage profile for each profile test. Both test classes use [SqlServerFixture](SqlServerFixture.cs), each with its own container. It defaults to `mcr.microsoft.com/mssql/server:2022-latest`, with a random password and host port and a private Docker network. Profiles are adapted for SQL authentication and the disposable server's self-signed certificate; passwords are supplied separately.

`ReferencedDeploymentScriptTests.cs` preserves the four cases from #987: deployment succeeds or fails, with referenced scripts enabled or disabled. It verifies that referenced post-deployment scripts run only after successful deployment with `RunScriptsFromReferences` enabled, checks the resulting database objects and marker rows, and asserts the deployment return code and failure output. These tests build their own small DACPACs and require only Docker and the .NET SDK.

## Run locally

Use a current .NET 10 SDK, an x64 host with a local Docker daemon running Linux containers, and SqlPackage. CI uses SqlPackage 170.5.96, which requires .NET runtime 10.0.11 or later.

From the repository root:

```bash
dotnet tool install --global Microsoft.SqlPackage --version 170.5.96
dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release
```

To run only the referenced deployment script tests (no SqlPackage installation required):

```bash
dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release --filter TestCategory=SqlServer
```

SQL Server startup, readiness, credentials, and cleanup are managed by Testcontainers.

Set `SQLPROJ_TEST_SQLSERVER_IMAGE` to run the same tests against a different SQL Server image. For example, in Bash:

```bash
SQLPROJ_TEST_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2025-latest \
  dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release
```

Each local test run uses one image. To reproduce both CI matrix entries locally, run them sequentially (Bash):

```bash
for sqlserver_version in 2022 2025; do
  SQLPROJ_TEST_SQLSERVER_IMAGE="mcr.microsoft.com/mssql/server:${sqlserver_version}-latest" \
    dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release \
      --logger trx --results-directory "artifacts/integration-tests-${sqlserver_version}" || exit 1
done
```

The default is `2022-latest`; CI runs the suite against both `2022-latest` and `2025-latest`. These tags track updates within each major version. An explicit image tag can also be supplied to reproduce a particular run. The test DACPACs still target `Sql160`, so the 2025 run checks deployment compatibility with that server, not SQL Server 2025-specific target-platform features.

Azure SQL Database container coverage is a separate follow-up. Its [private preview](https://microsoft.github.io/azure-sql-database-container/getting-started.html) requires registry credentials and compatibility checks for DACPAC targets and database setup/cleanup. Selecting an Azure SQL image alone does not establish support for these tests.

For the full suite, SqlPackage must be on `PATH`; set `SQLPACKAGE_PATH` to use a different executable location. Initial runs need network access to restore packages and download container images and SqlPackage for the deployment image. Missing prerequisites cause failures rather than skipped tests.

The fixture removes its containers, network, temporary files, and generated deployment image after the run. Downloaded dependency images remain cached. The container test intentionally uses a bind mount to exercise supplying a profile at container runtime, so it requires a local Docker daemon.

The `integration-tests` CI matrix runs both test classes on SQL Server 2022 and 2025. Each matrix entry uploads separately named TRX artifacts, and both must pass before release publishing. The workflow’s existing end-to-end `deploy-*` jobs remain separate and continue to use their own SQL Server containers.
