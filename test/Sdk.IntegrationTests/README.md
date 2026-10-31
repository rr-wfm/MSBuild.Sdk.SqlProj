# SDK integration tests

This suite tests SDK builds, direct DACPAC references, pre-plan scripts, ER diagrams, tool invocation, and package layout. Docker, SQL Server, and SqlPackage are not required.

Use the .NET 10 SDK. The tests target .NET 8, 9, and 10; install all three runtimes or add `-f net10.0` to run only .NET 10. Initial runs need network access to restore packages. Mermaid rendering checks are optional and skip when `mmdc` is unavailable.

```bash
dotnet test test/Sdk.IntegrationTests/Sdk.IntegrationTests.csproj -c Release
```

`SdkDefaultTests` verifies that a project without `SqlServerVersion` builds a DACPAC targeting SQL Server 2025. `PackLayoutTests` verifies that the generated NuGet package places DACPACs under `tools/`.

Run just these tests with:

```bash
dotnet test test/Sdk.IntegrationTests/Sdk.IntegrationTests.csproj -c Release --filter 'TestCategory=Build|TestCategory=Pack'
```

The build helpers in `test/IntegrationTestSupport/Build` are shared with the deployment suite and have no Docker dependencies.
