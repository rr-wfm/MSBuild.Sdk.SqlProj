# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo ships

An MSBuild SDK (`MSBuild.Sdk.SqlProj`) that compiles SDK-style .NET projects of `.sql` files into SQL Server Data-Tier Application packages (`.dacpac`), plus `dotnet new sqlproj` templates. The SDK is a thin MSBuild layer; all real work happens in a bundled CLI (`DacpacTool`) invoked from `Sdk.targets`.

## Three-project layout

- `src/DacpacTool` — .NET CLI (`net8.0`) that builds, inspects, and deploys dacpacs. Uses `Microsoft.SqlServer.DacFx`, `Microsoft.SqlServer.SqlManagementObjects`, and `DotMake.CommandLine`. Subcommands: `build`, `collect-includes`, `deploy` (see `Program.cs` and the `*Options.cs` files).
- `src/MSBuild.Sdk.SqlProj` — the NuGet SDK package. The packaging-only `MSBuild.Sdk.SqlProj.msbuildproj` uses `Microsoft.Build.NoTargets` (which imports `Microsoft.NET.Sdk`) with package metadata and file mappings declared in the project, without a custom `.nuspec`. A build-only project reference restores and builds the CLI; a post-Build `MSBuild` task (`IncludeDacpacTool`) publishes it into `tools/DacpacTool/`. Only that tool directory is packaged alongside `Sdk/Sdk.props` and `Sdk/Sdk.targets`. Tool files are collected at pack time so newly published files are included.
- `src/MSBuild.Sdk.SqlProj.Templates` — `dotnet new` templates. The packaging-only `MSBuild.Sdk.SqlProj.Templates.msbuildproj` uses `Microsoft.Build.NoTargets` with package metadata and `content` file mappings declared in the project, without a custom `.nuspec`.

## How the SDK wires up at build time

When a consumer project uses this SDK, `Sdk.targets` does the following — important when changing build behavior:

1. **`ValidateEnvironment`** locates the single `net8.0` DacpacTool payload independently of the SDK in use. The CLI project sets `RollForward=Major`, which is written into its runtime configuration and supports .NET 8/9/10 without separate payloads or command-line roll-forward flags. `DacpacToolExe` can be overridden via `-p:DacpacToolExe=...` (used by tests).
2. **`ResolveDatabaseReferences`** turns each `<PackageReference>` into a dacpac path by reading the auto-generated `Pkg<package-id>` property (dots → underscores). Falls back to `$(NuGetPackageRoot)/<id>/<version>` if that property isn't set. Project references resolve from `_ResolvedProjectReferencePaths`. This is the first place to look for reference-resolution bugs.
3. **`CoreCompile`** writes `$(MSBuildProjectName).InputFiles.txt` and `WarningsSuppression.txt` to `obj/`, then shells out to `dotnet DacpacTool.dll build` with a long argument list. Inputs include `@(Content)`, pre/post-deploy scripts, refactor log, and the list of `--include`-resolved files from a prior `collect-includes` invocation (`GetIncludedFiles` target).
4. **`PublishDatabase`** invokes the CLI's `deploy` subcommand. `Publish` from inside Visual Studio chains this; from CLI you call `/t:PublishDatabase` explicitly.
5. **`KnownModelProperties`** / **`KnownDeployProperties`** in `Sdk.props` enumerate which MSBuild properties get forwarded to the CLI as `-bp` (model) or `-dp` (deploy) flags. Adding a new DacFx property requires updating these lists.

## Common commands (run from repo root)

```bash
# Build the SDK and publish the CLI into src/MSBuild.Sdk.SqlProj/tools/DacpacTool/ for downstream test projects
dotnet build ./src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.msbuildproj -c Release

# Analyzer/style check that CI enforces
dotnet format analyzers ./MSBuild.Sdk.SqlProj.slnx --verify-no-changes --severity info

# Unit tests (MSTest + Shouldly + NSubstitute, multi-TFM)
dotnet test ./test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release

# Run a single test
dotnet test ./test/DacpacTool.Tests/DacpacTool.Tests.csproj --filter "FullyQualifiedName~PackageBuilderTests.SomeTestName"

# Build a representative integration scenario
dotnet build ./test/TestProject/TestProject.csproj -c Release

# Pack the SDK
dotnet pack -c Release ./src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.msbuildproj
```

CI (`.github/workflows/main.yml`) runs the above sequence, then a matrix across Ubuntu/macOS/Windows × .NET 8/9/10 building `TestProjectWithSDKRef` against the packed SDK, then three deploy jobs (`deploy-sqlpackage`, `deploy-publish`, `deploy-container`) against `mcr.microsoft.com/mssql/server:2022-latest`. Publishing to NuGet only fires for `release/*` branches.

## Iterating on changes locally

The `test/TestProject*` projects import `Sdk.props` and `Sdk.targets` **by path** rather than via `<Project Sdk="...">`, so they pick up local changes immediately after you rebuild the DacpacTool/SDK pair — no package push needed. Exceptions: `TestProjectWithSDKRef` and `TestProjectWithPackageReference` reference NuGet packages; read the comments at the top of each csproj for how to build them.

To attach a debugger to the CLI when it's invoked from MSBuild, pass `/p:MSBuildSdkSqlProjDebug=True` to `dotnet build` — the tool will block on `Debugger.IsAttached` (DEBUG builds only; see `Program.WaitForDebuggerToAttach`).

## Versioning

Nerdbank.GitVersioning (`version.json`, current `4.3.0-beta.{height}`). CI runs `nbgv cloud` early; locally `dotnet build` will compute a height-based version automatically.

## Where to make changes

- SDK build/publish/reference behavior → `src/MSBuild.Sdk.SqlProj/Sdk/Sdk.targets` (and the `KnownModelProperties` / `KnownDeployProperties` lists in `Sdk.props`).
- CLI command surface / option parsing → `src/DacpacTool/Program.cs` + the relevant `*Options.cs`. Commands are wired through `DotMake.CommandLine` attributes on `RootCommand`.
- Package building, validation, scripts → `PackageBuilder.cs`, `PackageAnalyzer.cs`, `ScriptParser.cs`, `ScriptInspector.cs`, `IncludeVariableResolver.cs`.
- Deployment → `PackageDeployer.cs`.
- ER diagram generation → `src/DacpacTool/Diagram/`.
- Templates → `src/MSBuild.Sdk.SqlProj.Templates/templates/`.

## Style and constraints

- C# style is analyzer-enforced for DacpacTool: `AnalysisMode=all`, `EnforceCodeStyleInBuild=true`, `Nullable=warnings`. `dotnet format analyzers --verify-no-changes --severity info` must pass.
- Cross-platform: Windows, macOS, Linux are all CI-validated. Avoid Windows-only path/process assumptions.
- The repo has many scenario test projects that exercise specific MSBuild behaviors (warnings as errors, suppression, SqlCmdVar, package/project refs, pre/post-deploy, master/server refs, etc.). When changing SDK targets, build at least one relevant `test/TestProject*` in addition to running unit tests — unit tests do not exercise the MSBuild target graph.
