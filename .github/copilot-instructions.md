# Copilot instructions for MSBuild.Sdk.SqlProj

## Repository purpose and layout
- This repository ships an MSBuild SDK that builds SQL projects into `.dacpac` artifacts.
- Core projects:
  - `./src/DacpacTool`: CLI used by SDK targets to build/inspect/deploy dacpacs.
  - `./src/MSBuild.Sdk.SqlProj`: SDK packaging and MSBuild `Sdk.props`/`Sdk.targets`.
  - `./src/MSBuild.Sdk.SqlProj.Templates`: `dotnet new` templates.
- Tests and scenario projects:
  - `./test/DacpacTool.Tests`: unit tests for CLI logic.
  - `./test/TestProject*`: integration-style build/publish scenarios.

## Where to change code
- SDK behavior (build/publish/reference resolution):
  - `./src/MSBuild.Sdk.SqlProj/Sdk/Sdk.targets`
  - `./src/MSBuild.Sdk.SqlProj/Sdk/Sdk.props`
- CLI behavior and command handling:
  - `./src/DacpacTool/Program.cs`
  - `./src/DacpacTool/*.cs`
- Template changes:
  - `./src/MSBuild.Sdk.SqlProj.Templates/templates/sqlproj`

## Preferred validation sequence
Run from the repository root.

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
- Primary CI workflow: `./.github/workflows/main.yml`.
- CI runs across Ubuntu/macOS/Windows and .NET 8/9/10 for SDK reference scenarios.
- CI also validates deployment flows using SQL Server containers (`deploy-sqlpackage`, `deploy-publish`, `deploy-container`).
- Keep local validation focused first; use workflow logs to debug matrix/platform-specific failures.

## Coding and style expectations
- Follow `./.editorconfig`.
- C# style is analyzer-enforced (`AnalysisMode=all`, `EnforceCodeStyleInBuild=true` in DacpacTool).
- Avoid broad refactors unless requested; this repo has many scenario tests that can be sensitive to behavioral changes.

## Practical tips for efficient agent work
- Prefer surgical changes in one subsystem at a time (DacpacTool vs SDK targets vs templates).
- When modifying SDK target behavior, validate at least one relevant `test/TestProject*` scenario build in addition to unit tests.
- For package/version resolution issues, inspect `ResolveDatabaseReferences` and `ValidateEnvironment` logic in `Sdk.targets` first.
- Do not edit generated outputs under `bin/` or `obj/`; only source and test inputs.
