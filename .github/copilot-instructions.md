# Copilot instructions for MSBuild.Sdk.SqlProj

## Repository purpose and layout
- This repository ships an MSBuild SDK that builds SQL projects into `.dacpac` artifacts.
- Core projects:
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/DacpacTool`: CLI used by SDK targets to build/inspect/deploy dacpacs.
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/MSBuild.Sdk.SqlProj`: SDK packaging and MSBuild `Sdk.props`/`Sdk.targets`.
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/MSBuild.Sdk.SqlProj.Templates`: `dotnet new` templates.
- Tests and scenario projects:
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/test/DacpacTool.Tests`: unit tests for CLI logic.
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/test/TestProject*`: integration-style build/publish scenarios.

## Where to change code
- SDK behavior (build/publish/reference resolution):
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/MSBuild.Sdk.SqlProj/Sdk/Sdk.targets`
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/MSBuild.Sdk.SqlProj/Sdk/Sdk.props`
- CLI behavior and command handling:
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/DacpacTool/Program.cs`
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/DacpacTool/*.cs`
- Template changes:
  - `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/src/MSBuild.Sdk.SqlProj.Templates/templates/sqlproj`

## Preferred validation sequence
Run from `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj`.

1. Build CLI:
   - `dotnet build ./src/DacpacTool/DacpacTool.csproj -c Release`
2. Analyzer/style verification:
   - `dotnet format analyzers ./MSBuild.Sdk.SqlProj.slnx --verify-no-changes --severity info`
3. Unit tests:
   - `dotnet test ./test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release`
4. SDK build:
   - `dotnet build ./src/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj.csproj -c Release`
5. Integration sanity scenarios commonly used in CI:
   - `dotnet build ./test/TestProject/TestProject.csproj -c Release`
   - `dotnet build ./test/TestProjectWithAnalyzers/TestProjectWithAnalyzers.csproj -c Release`
   - `dotnet build ./test/TestProjectWithGenerateScriptAndTargetDatabaseName/TestProjectWithGenerateScriptAndTargetDatabaseName.csproj -c Release`

## CI alignment
- Primary CI workflow: `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/.github/workflows/main.yml`.
- CI runs across Ubuntu/macOS/Windows and .NET 8/9/10 for SDK reference scenarios.
- CI also validates deployment flows using SQL Server containers (`deploy-sqlpackage`, `deploy-publish`, `deploy-container`).
- Keep local validation focused first; use workflow logs to debug matrix/platform-specific failures.

## Coding and style expectations
- Follow `/home/runner/work/MSBuild.Sdk.SqlProj/MSBuild.Sdk.SqlProj/.editorconfig`.
- C# style is analyzer-enforced (`AnalysisMode=all`, `EnforceCodeStyleInBuild=true` in DacpacTool).
- Avoid broad refactors unless requested; this repo has many scenario tests that can be sensitive to behavioral changes.

## Practical tips for efficient agent work
- Prefer surgical changes in one subsystem at a time (DacpacTool vs SDK targets vs templates).
- When modifying SDK target behavior, validate at least one relevant `test/TestProject*` scenario build in addition to unit tests.
- For package/version resolution issues, inspect `ResolveDatabaseReferences` and `ValidateEnvironment` logic in `Sdk.targets` first.
- Do not edit generated outputs under `bin/` or `obj/`; only source and test inputs.

## Errors encountered during onboarding and workarounds
- Encountered error while running full multi-target test command:
  - Command: `dotnet test ./test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release`
  - Observed: `MSB4181: The "VSTestTask" task returned false but did not log an error.`
- Follow-up diagnostic run on a single target framework produced actionable failure details:
  - Command: `dotnet test ./test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release -f net10.0 -v minimal`
  - Failure: `VersionCheckerTests.RunsVersionCheck` expected one warning line but got zero.
- Likely cause in cloud/sandbox environments:
  - `VersionChecker` calls `https://api.github.com/repos/rr-wfm/MSBuild.Sdk.SqlProj/releases/latest` with a short timeout and swallows exceptions, so restricted/unstable network can make this test fail non-deterministically.
- Workaround used for diagnosis and iterative validation:
  - Run targeted framework tests (`-f net10.0`) to get concrete test output.
  - If blocked by outbound network restrictions, use test filtering to continue validating unrelated changes, then re-run full tests in CI or a network-enabled environment.
