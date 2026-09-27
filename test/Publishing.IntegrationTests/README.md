# Publishing integration tests

This suite tests publishing through the SDK's MSBuild targets and SqlPackage against a disposable SQL Server. Run it when changing deployment behavior or container publishing.

## Scope

`PublishingTests.cs` runs four cases:

| Case | What it verifies |
| --- | --- |
| Built-in publisher | `dotnet publish /t:PublishDatabase` deploys using MSBuild connection and database properties. |
| Local SqlPackage profile | SqlPackage deploys using the generated profile's target settings and leaves the profile unchanged. |
| Database override | `/TargetDatabaseName` overrides the profile's database name and leaves the profile unchanged. |
| Container profile | `PublishContainer` builds a deployment image that successfully publishes using a read-only mounted profile. |

Every case queries the resulting database to verify `dbo.MyTable` has exactly the expected columns: `Column1` (`nvarchar`) and `Column2` (`int`).

The shared [fixture](PublishingFixture.cs) copies the repository's [TestProject](../TestProject/TestProject.csproj) into a temporary directory, uses the SDK and DacpacTool from the checkout, and writes a minimal SqlPackage profile for each profile test. It does not depend on a profile-generation template. It runs SQL Server 2022 CU27 to match the project's `Sql160` target, with a random password and host port and a private Docker network. Profiles are adapted for SQL authentication and the disposable server's self-signed certificate; passwords are supplied separately.

`ReferencedScriptContextTests.cs` adds ten regression tests for the behavior described in [issue #996](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues/996). They verify which database referenced pre/post-deployment scripts run in for new and existing targets, including login defaults, explicit database selection, and disabling referenced scripts. These tests preserve the current behavior.

This suite covers creating a database from a simple DACPAC and database context for referenced scripts. It does not currently cover profile-template generation, schema upgrades, data-loss protection, user-defined SQLCMD variables, the main package's pre/post-deployment scripts, context changes between multiple references, other authentication methods, or other SQL Server versions. The separate [DacpacTool tests](../DacpacTool.Tests/DeploymentIntegrationTests.cs) cover referenced post-deployment script behavior.

## Run locally

Use a current .NET 10 SDK, an x64 host with a local Docker daemon running Linux containers, and SqlPackage. CI uses SqlPackage 170.5.96, which requires .NET runtime 10.0.11 or later.

From the repository root:

```bash
dotnet tool install --global Microsoft.SqlPackage --version 170.5.96
dotnet test test/Publishing.IntegrationTests/Publishing.IntegrationTests.csproj -c Release
```

SqlPackage must be on `PATH`; set `SQLPACKAGE_PATH` to use a different executable location. Initial runs need network access to restore packages and download container images and SqlPackage for the deployment image. Missing prerequisites cause failures rather than skipped tests.

The fixture removes its containers, network, temporary files, and generated deployment image after the run. Downloaded dependency images remain cached. The container test intentionally uses a bind mount to exercise supplying a profile at container runtime, so it requires a local Docker daemon.

The `publishing-integration` CI job runs this suite, uploads TRX results, and must pass before release publishing.
