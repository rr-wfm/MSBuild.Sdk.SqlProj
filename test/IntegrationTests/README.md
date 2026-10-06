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

The shared [fixture](PublishingFixture.cs) copies the repository's [TestProject](../TestProject/TestProject.csproj) into a temporary directory, uses the SDK and DacpacTool from the checkout, and writes a minimal SqlPackage profile for each profile test. Both test classes use [SqlServerFixture](SqlServerFixture.cs), each with its own container. It runs SQL Server 2022 CU27 to match the project's `Sql160` target, with a random password and host port and a private Docker network. Profiles are adapted for SQL authentication and the disposable server's self-signed certificate; passwords are supplied separately.

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

For the full suite, SqlPackage must be on `PATH`; set `SQLPACKAGE_PATH` to use a different executable location. Initial runs need network access to restore packages and download container images and SqlPackage for the deployment image. Missing prerequisites cause failures rather than skipped tests.

The fixture removes its containers, network, temporary files, and generated deployment image after the run. Downloaded dependency images remain cached. The container test intentionally uses a bind mount to exercise supplying a profile at container runtime, so it requires a local Docker daemon.

The `integration-tests` CI job runs both test classes, uploads TRX results, and must pass before release publishing. The workflow’s existing end-to-end `deploy-*` jobs remain separate and continue to use their own SQL Server containers.
