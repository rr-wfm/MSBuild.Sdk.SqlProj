# Packaged deployment tests

This suite tests database deployment using the built MSBuild.Sdk.SqlProj NuGet package. It uses [TestProjectWithSDKRef](../TestProjectWithSDKRef/TestProjectWithSDKRef.csproj), which references the [TestProject](../TestProject/TestProject.csproj) dependency package, and deploys to disposable SQL Server containers managed by Testcontainers.

| Category | What it tests |
| --- | --- |
| `PackagedSqlPackage` | Deploys the supplied DACPAC through SqlPackage with `IncludeCompositeObjects=True`. |
| `PackagedPublish` | Builds with the supplied SDK and dependency packages, then deploys using `dotnet publish /t:PublishDatabase`. |
| `PackagedContainer` | Builds a deployment image with the supplied packages using `PublishContainer`, then runs it against SQL Server. |

Each case checks successful deployment and seeded rows in the project's `dbo.TestTable` and the dependency's `dbo.MyTable`, exercising composite objects and the included post-deployment script.

## How CI runs it

The [main workflow](../../.github/workflows/main.yml) builds one SDK package, dependency package, and DACPACs targeting SQL Server 2025 (`Sql170`). The consuming project's DACPAC is uploaded after the exact-version build, before floating-version builds overwrite it.

The `deploy-sqlpackage`, `deploy-publish`, and `deploy-container` jobs call the [packaged deployment workflow](../../.github/workflows/packaged-deployment.yml). Each runs its corresponding test category against SQL Server 2025 with the matching artifacts. Results are uploaded separately for each category, including when tests fail.

Release publishing requires `deploy-sqlpackage` and `deploy-publish` to pass, along with the separate [integration test suite](../IntegrationTests/README.md). `deploy-container` runs in CI but is not a release prerequisite.

## Run locally

Use an x64 host, Docker running Linux containers, and the .NET 8, 9, and 10 SDKs to prepare the packages. The SqlPackage test also requires SqlPackage on `PATH`; set `SQLPACKAGE_PATH` to use another installation. Package restores and container downloads require network access.

From the repository root, run:

```bash
dotnet tool install --global Microsoft.SqlPackage
sqlpackage /Version
bash test/PackagedDeploymentIntegrationTests/prepare-artifacts.sh
```

Check that `sqlpackage /Version` succeeds before preparing artifacts. SqlPackage may require a newer .NET runtime patch than the one bundled with your installed SDK. If it reports a missing framework, install the requested runtime into the .NET location shown in the error, then retry. Use `dotnet --list-runtimes` to check installed versions.

For a script-managed installation in `~/.dotnet`, you can update the .NET 10 runtime with the [Microsoft installation script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script):

```bash
bash "$HOME/.dotnet/dotnet-install.sh" \
  --channel 10.0 --runtime dotnet --install-dir "$HOME/.dotnet"
```

This assumes the installation script is already saved at that path. For a package-manager installation, update the runtime through that package manager instead.

The artifact preparation script creates a fresh directory under `artifacts/`, packs the SDK and dependency, builds the consuming project, and prints the exact `dotnet test` command. Run that printed command to execute all three tests against SQL Server 2025.

To use artifacts you already have:

```bash
SQLPROJ_TEST_ARTIFACTS=/absolute/path/to/prepared-artifacts \
  dotnet test test/PackagedDeploymentIntegrationTests/PackagedDeploymentIntegrationTests.csproj -c Release \
  --logger trx --results-directory artifacts/packaged-results
```

Add `--filter TestCategory=PackagedSqlPackage`, `--filter TestCategory=PackagedPublish`, or `--filter TestCategory=PackagedContainer` to run one path.

## Artifact requirements

The directory supplied through `SQLPROJ_TEST_ARTIFACTS` must contain:

```text
packages/
  MSBuild.Sdk.SqlProj.<version>.nupkg
  TestProject.<version>.nupkg
dacpac/
  TestProjectWithSDKRef.dacpac
  TestProject.dacpac
```

Provide exactly one package for each ID. Tests read versions from the package manifests and use a private package cache and source mappings to resolve the supplied packages.

The dependency package and DACPACs must target SQL Server 2025 (`Sql170`). The fixture rejects mismatches.

## Diagnostics and cleanup

Test results include process output, SQL Server logs, deployment-container logs, and build/container-publish binary logs. Text logs redact the generated password. Binary logs are collected only for builds without database credentials; the credential-bearing `PublishDatabase` command produces redacted text output instead.

The shared [SQL Server fixture](../IntegrationTestSupport/SqlServerFixture.cs) manages startup, readiness, random credentials and ports, and a private Docker network. The [process helper](../IntegrationTestSupport/IntegrationTestProcess.cs) handles command timeouts and log capture.

Cleanup removes containers, networks, generated deployment images, private package caches, and temporary project copies, including after failed initialization or deployment. Supplied artifacts, test results, and cached dependency images remain available.
