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

The shared [fixture](PublishingFixture.cs) copies the repository's [TestProject](../TestProject/TestProject.csproj) into a temporary directory, uses the SDK and DacpacTool from the checkout, and writes a minimal SqlPackage profile for each profile test. Each test class uses [SqlServerFixture](../IntegrationTestSupport/SqlServerFixture.cs) with its own container. It defaults to `mcr.microsoft.com/mssql/server:2025-latest`, with a random password and host port and a private Docker network. Profiles are adapted for SQL authentication and the disposable server's self-signed certificate; passwords are supplied separately.

`ReferencedDeploymentScriptTests.cs` preserves the four cases from #987: deployment succeeds or fails, with referenced scripts enabled or disabled. It verifies that referenced post-deployment scripts run only after successful deployment with `RunScriptsFromReferences` enabled, checks the resulting database objects and marker rows, and asserts the deployment return code and failure output. These tests build their own small DACPACs and require only Docker and the .NET SDK.

`ReferencedScriptContextTests.cs` adds ten regression tests for the behavior described in [issue #996](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues/996). They verify which database referenced pre/post-deployment scripts run in for new and existing targets, including login defaults, explicit database selection, and disabling referenced scripts. These tests preserve the current behavior and use the SQL Server 2025 target platform.

This suite covers creating a database from a simple DACPAC and database context for referenced scripts. It does not currently cover profile-template generation, schema upgrades, data-loss protection, user-defined SQLCMD variables, the main package's pre/post-deployment scripts, context changes between multiple references, or other authentication methods.

## Run locally

Use a current .NET 10 SDK, an x64 host with a local Docker daemon running Linux containers, and SqlPackage. CI installs the latest stable SqlPackage version without pinning a specific version.

From the repository root:

```bash
dotnet tool install --global Microsoft.SqlPackage
dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release
```

To run only the referenced deployment script tests (no SqlPackage installation required):

```bash
dotnet test test/IntegrationTests/IntegrationTests.csproj -c Release --filter TestCategory=SqlServer
```

SQL Server startup, readiness, credentials, and cleanup are managed by Testcontainers.

Local runs and CI use SQL Server 2025 with the `Sql170` DACPAC target platform.

Before building, `PublishingFixture` sets `<SqlServerVersion>` to `Sql170` in its temporary copy of `test/TestProject/TestProject.csproj`. The referenced-script tests use the same platform when building their DACPACs programmatically.

Azure SQL Database container coverage is a separate follow-up. Its [private preview](https://microsoft.github.io/azure-sql-database-container/getting-started.html) requires registry credentials and compatibility checks for DACPAC targets and database setup/cleanup. Selecting an Azure SQL image alone does not establish support for these tests.

For the full suite, SqlPackage must be on `PATH`; set `SQLPACKAGE_PATH` to use a different executable location. Initial runs need network access to restore packages and download container images and SqlPackage for the deployment image. Missing prerequisites cause failures rather than skipped tests.

The fixture removes its containers, network, temporary files, and generated deployment image after the run. Downloaded dependency images remain cached. The container test intentionally uses a bind mount to exercise supplying a profile at container runtime, so it requires a local Docker daemon.

The `integration-tests` CI job runs the full suite on SQL Server 2025, uploads TRX artifacts, and must pass before release publishing. The separate `deploy-*` jobs run the [packaged deployment tests](../PackagedDeploymentIntegrationTests/README.md) using their own SQL Server containers.
