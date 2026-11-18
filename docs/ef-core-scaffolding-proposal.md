# Generate EF Core models when building a SQL project

## Why

When the SQL project is the source of truth for the database schema, the EF Core model can fall behind. [EF Core Power Tools](https://github.com/ErikEJ/EFCorePowerTools/blob/master/src/Core/efcpt.10/readme.md) can already generate a DbContext and entity classes from a `.dacpac` without SQL Server. Making this an opt-in feature of MSBuild.Sdk.SqlProj would make it easier for existing SDK users to adopt: configure it in the SQL project and keep using the normal build, without adding a separate integration package.

## Enable it in the SQL project

Add the following to the project file that already uses `MSBuild.Sdk.SqlProj`. The item opts the SQL project in and explicitly identifies the C# project that receives the generated files. The item and metadata names are proposed; they do not work in current releases.

```xml
<ItemGroup>
  <EfCoreScaffold Include="..\DataAccess\DataAccess.csproj">
    <ConfigFile>..\DataAccess\efcpt-config.json</ConfigFile>
  </EfCoreScaffold>
</ItemGroup>
```

Both paths are relative to the SQL project. The SDK uses the directory containing `DataAccess.csproj` as the output root. EF Core Power Tools can place the generated files in a subfolder such as `Models` under that root, according to its config. The first release would support one target C# project and one DbContext per SQL project. There is no solution scanning and no scaffolding-specific package reference in the C# project.

Check in `efcpt-config.json` and a .NET local tool manifest that pins `ErikEJ.EFCorePowerTools.Cli`. After `dotnet tool restore`, build the SQL project as usual. The SDK builds or reuses the DACPAC, checks whether generation is needed, and invokes EF Core Power Tools when required. For solution builds, declare a build-order dependency from `DataAccess` to the SQL project (not an assembly reference). A clean solution build should compile the generated sources on the first attempt, even if MSBuild evaluated the C# project's file list before generation. Building `DataAccess` in isolation without building the SQL project does not refresh its models.

## When does it regenerate?

| Change | Expected behavior |
| --- | --- |
| First build, missing generated files, or a requested refresh | Generate. |
| Schema changes in the source DACPAC or in a referenced DACPAC used by the generator | Regenerate. |
| Config, renaming rules, templates, or the resolved CLI version changes | Regenerate, even if SQL compilation is skipped. |
| Only packaging metadata or deployment scripts change, with no schema change | Skip generation. |
| Nothing relevant changes and generated files are present | Skip generation. |

The SDK would check freshness independently of SQL compilation and skip generation during Visual Studio design-time builds. It would only remove obsolete files that it previously generated, leaving handwritten code alone. A missing tool or generator failure would fail the opted-in build with an actionable diagnostic and would not mark generation as up to date.

## Tool version

Pin an exact CLI version by default so local builds and CI use the same generator. Teams that prefer the latest stable version can opt into an explicit tool update step. Normal builds would not check for updates or modify global tool installations. Updating the resolved version triggers regeneration; choose a version compatible with the project's EF Core version and installed .NET runtime.

## Prior art and scope

[JD.Efcpt.Build](https://github.com/JerrettDavis/JD.Efcpt.Build) demonstrates incremental EF Core scaffolding during builds. This proposal aims to make the DACPAC workflow easier to adopt directly in MSBuild.Sdk.SqlProj. A smoke test using EF Core Power Tools CLI `10.1.1620` generated an entity and a DbContext from this repository's `TestProject.dacpac` without SQL Server; the SDK build integration is not implemented yet.

Live-database scaffolding, automatic solution discovery, and changes to database publishing are out of scope.

## Feedback wanted

- Is a C# project path plus an `efcpt-config.json` path enough to configure your usual setup?
- Do you depend on referenced DACPACs for scaffolding? If so, how are those references supplied to your SQL project?

