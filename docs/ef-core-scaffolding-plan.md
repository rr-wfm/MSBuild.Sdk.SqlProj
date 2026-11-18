# Build-time EF Core scaffolding from a DACPAC

Status: Proposed; no implementation yet. Aligned with the [feature proposal](ef-core-scaffolding-proposal.md).

## Goal and user experience

Make EF Core model generation an opt-in feature of MSBuild.Sdk.SqlProj, configured in the SQL project and executed by its normal build. Use EF Core Power Tools CLI (`efcpt`) against the built DACPAC without a connection string or live SQL Server. Ship the integration in the existing SDK package, with no separate integration package or end-user DacpacTool invocation.

The SQL project explicitly identifies the target C# project and CLI config using this proposed syntax:

```xml
<ItemGroup>
  <EfCoreScaffold Include="..\DataAccess\DataAccess.csproj">
    <ConfigFile>..\DataAccess\efcpt-config.json</ConfigFile>
  </EfCoreScaffold>
</ItemGroup>
```

Both paths are relative to the SQL project. Generate using the target C# project directory as the output root, with subfolders such as `Models` controlled by the CLI config. Do not generate source into `obj`; reserve intermediate storage for fingerprints and generation state. Support one target C# project and one DbContext per SQL project initially.

After restoring the pinned local tool, building the SQL project builds or reuses its DACPAC and checks whether models need regenerating. Solution builds declare a build-order dependency from the C# project to the SQL project, not an assembly reference. Building the C# project alone without building the SQL project does not refresh its models. No scaffolding-specific package reference is required in the C# project.

## Implementation phases

### 1. Prove build ordering and define the integration contract

- [ ] Prototype an SDK target that runs after the DACPAC and required reference files are available, independently of whether SQL `CoreCompile` executes. Do not make generation a SQL compilation option. Resolve actual MSBuild output paths instead of guessing a `bin` path.
- [ ] Prove that a clean solution build compiles newly generated sources in the same invocation. Build ordering alone does not refresh C# globs evaluated before generation: determine and document the minimal supported compile-item integration without requiring a separate package. Do not claim this requirement is met until a fixture with no pre-existing generated files passes. If a consumer-side import or explicit compile configuration is necessary, document that requirement and update the proposal before implementation proceeds.
- [ ] Validate the target project and config paths with actionable errors. Reject multiple items initially rather than silently selecting one. Treat project path plus config path as the generation identity so later support for multiple runs can retain the same XML format; do not deduplicate items by project path alone.
- [ ] Skip design-time builds. Verify Visual Studio fast up-to-date behavior for config-only edits and missing generated output, and define multi-targeting and parallel-build behavior without concurrent writes to the same source-tree files.

### 2. Integrate EF Core Power Tools

- [ ] Implement SDK-owned orchestration, reusing DacpacTool internals where appropriate. Any internal command boundary is an implementation detail, not a standalone user workflow or a documented addition to `DacpacTool build`.
- [ ] Invoke a restored local .NET tool manifest with an exact `ErikEJ.EFCorePowerTools.Cli` version by default. Resolve the manifest predictably and document `dotnet tool restore` and an explicit latest-stable update workflow. Normal builds must not install tools, query for updates, or modify global tool installations. Report missing tools, runtime requirements, and EF Core compatibility clearly; record the version actually invoked.
- [ ] Pass absolute DACPAC, config, and output paths with a predictable working directory rooted at the C# project. Respect CLI renaming and template settings. Use a subprocess argument list and surface diagnostics and nonzero exit codes through the opted-in build.
- [ ] Verify which referenced DACPACs the CLI actually consumes and how it locates them. Make reference resolution deterministic without assuming every SQL compilation reference is a scaffold input.

### 3. Add independent regeneration checks and safe output ownership

- [ ] Fingerprint normalized `model.xml` rather than the whole DACPAC ZIP, including reference models actually consumed by scaffolding. Packaging metadata and deployment-script-only changes must not trigger generation when the schema is unchanged. Use JD.Efcpt.Build's normalization approach as prior art, not as a required dependency.
- [ ] Include config, renaming rules, templates, effective options, resolved CLI version, and a fingerprint format version. Detect missing inputs explicitly rather than treating them as empty content.
- [ ] Keep generation state in the SQL project's intermediate directory, keyed by normalized target project and config paths. Track owned output files under the C# project root. Skip only when inputs match and tracked outputs exist. Provide an SDK-level way to force regeneration.
- [ ] Mark a run current only after successful generation and output validation. Atomically write the manifest; record the final config if the CLI rewrites it. Failed or interrupted runs must remain stale and retry on the next build.
- [ ] Remove obsolete files only when they were previously generated and are inside the permitted output locations. Preserve handwritten partial classes and unrelated source files. Define a safe first-run adoption policy for existing generated files, protect against output collisions, and avoid arbitrary project-root cleanup.
- [ ] Preserve generated source on normal clean operations unless explicitly requested otherwise; these are project files that users may check in. Losing intermediate state must trigger safe regeneration, not deletion of untracked files. Coordinate builds across configurations so they cannot overwrite shared source files concurrently.

### 4. Document and validate the user workflow

- [ ] Document the SQL-project opt-in XML, relative path rules, source-root output and CLI-controlled subfolders, initial single-context scope, build-order dependency, and any compile-item integration established in phase 1.
- [ ] Document pinned and latest-stable tool workflows, supported runtime and EF Core versions, reference DACPAC behavior, force refresh, generated-file ownership, and source-control guidance. State that isolated C# builds do not refresh models without building the SQL project.
- [ ] Add a minimal SQL producer and C# consumer scenario using the proposed setup. Run focused unit and integration validation, then the existing build and analyzer checks covering changed projects. Exercise the real pinned CLI on Windows and a Unix CI runner.

## Acceptance criteria

| Scenario | Expected result |
| --- | --- |
| First opted-in SQL-project build | Builds the DACPAC and generates one DbContext and its entities under the configured C# project root, without a database. |
| First clean solution build | Builds the SQL project before the consumer and compiles newly generated files in the same invocation, with no duplicate compile items. |
| Unchanged rebuild | Skips generation, including after an initial run that rewrote the CLI config. |
| Config, renaming, template, or resolved CLI version changes | Regenerates even when SQL compilation is skipped. |
| SQL model or consumed reference model changes | Regenerates. Repackaging or deployment-script-only changes with the same schema do not. |
| Missing generated output or forced refresh | Regenerates through the SDK build. |
| Invalid paths, unsupported multiple items, missing tool, or generator failure | Fails the opted-in build with an actionable diagnostic and does not record successful generation. A subsequent valid build can recover. |
| Schema object removed or excluded | Removes obsolete owned code without deleting handwritten or unrelated source files. |
| Clean or lost intermediate state | Preserves source-tree files; the next build safely regenerates and restores state. |
| Multi-targeting, parallel configurations, and Visual Studio | No concurrent output writes; no duplicate compilation items. Real builds respond to scaffold input changes and missing output; design-time builds do not run the generator. |
| C# build without building the SQL project | Does not refresh generated models. |
| No opt-in | Retains existing SQL build behavior and does not require EF Core Power Tools. |
| Existing publish flow | Keeps deployment semantics unchanged; the opted-in build still participates when publishing builds the project. |

Cover fingerprints, state transitions, path validation, and output ownership with focused unit tests in `test/DacpacTool.Tests` if orchestration resides in DacpacTool. Use a fake runner for deterministic tests and the real CLI for end-to-end scenarios. The clean solution, no-op rebuild, and config-only rebuild sequence is mandatory integration coverage.

## Scope and future extensibility

No standalone user-facing DacpacTool workflow, automatic solution discovery, live-database scaffolding, or changes to database deployment. Multiple target projects and multiple configs per project are deferred. The item format can later support one entry per generation run, keyed by project and config, with separate state and non-overlapping generated output locations.

## Feasibility evidence

EF Core Power Tools CLI `10.1.1620` was run against this repository's built `TestProject.dacpac` in a temporary directory. It generated `MyTable.cs` and `TestProjectContext.cs` without SQL Server. This verified DACPAC input, not MSBuild integration or incremental behavior.

- [EF Core Power Tools CLI documentation](https://github.com/ErikEJ/EFCorePowerTools/blob/master/src/Core/efcpt.10/readme.md)
- [JD.Efcpt.Build](https://github.com/JerrettDavis/JD.Efcpt.Build)
- [JD.Efcpt.Build DACPAC fingerprint implementation](https://github.com/JerrettDavis/JD.Efcpt.Build/blob/main/src/JD.Efcpt.Build.Tasks/DacpacFingerprint.cs)
