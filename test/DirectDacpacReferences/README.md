# Two producers, two direct DACPAC references

These are scratch SQL database projects targeting `net10.0` and requiring no SQL Server:

- `ProducerA` defines `dbo.Account`.
- `ProducerB` independently defines `dbo.[Order]` (no reference to ProducerA).
- `Consumer` defines `dbo.AccountOrder`, joining **both** tables as `[ProducerA].[dbo].[Account]` and `[ProducerB].[dbo].[Order]`. Each reference maps its producer to that database name with `DatabaseVariableLiteralValue`; its file path uses `$(TargetFramework)`. Its two dependencies are `ArtifactReference` items with logical producer identities and explicit `HintPath` file paths, not `ProjectReference` or NuGet dependencies. `HintPath` selects the file even when `Include` differs; an absent/empty hint uses `Include` as the file path. A missing selected file is an error, with no search/fallback.

From the repository root, using .NET SDK 10:

```sh
dotnet build src/DacpacTool/DacpacTool.csproj -c Release -f net10.0
TOOL="$PWD/src/DacpacTool/bin/Release/net10.0/DacpacTool.dll"

dotnet build test/DirectDacpacReferences/ProducerA/ProducerA.csproj -c Release -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror
dotnet build test/DirectDacpacReferences/ProducerB/ProducerB.csproj -c Release -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror
dotnet build test/DirectDacpacReferences/Consumer/Consumer.csproj -c Release -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror
```

The producers can be built in either order, but both must precede Consumer. Consumer's `bin/Release/net10.0` contains all three DACPACs. `bin`/`obj` artifacts are ignored by the repository's existing rules.

Negative checks (each command **must fail** with SQL71561):

```sh
# Removes only the Account reference: unresolved [ProducerA].[dbo].[Account].
dotnet build test/DirectDacpacReferences/Consumer/Consumer.csproj -c Release -t:Rebuild -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror -p:OmitProducerA=true
# Removes only the Order reference: unresolved [ProducerB].[dbo].[Order].
dotnet build test/DirectDacpacReferences/Consumer/Consumer.csproj -c Release -t:Rebuild -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror -p:OmitProducerB=true
# Restore the valid consumer output.
dotnet build test/DirectDacpacReferences/Consumer/Consumer.csproj -c Release -t:Rebuild -p:DacpacToolExe="$TOOL" -p:TreatTSqlWarningsAsErrors=True -warnaserror
```

Use Rebuild for the negative checks because command-line metadata/property changes alone are not tracked as incremental file inputs.

## Semantic validation and static lint

All three projects enable semantic validation and the same analyzer package/default rule selection as `src/MSBuild.Sdk.SqlProj.Templates/templates/sqlproj/sqlproj.csproj`:

```xml
<PropertyGroup>
  <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
  <TreatTSqlWarningsAsErrors>True</TreatTSqlWarningsAsErrors>
  <PageVerifyMode>Checksum</PageVerifyMode>
  <QueryStoreDesiredState>ReadWrite</QueryStoreDesiredState>
  <QueryStoreCaptureMode>Auto</QueryStoreCaptureMode>
  <TargetRecoveryTimePeriod>60</TargetRecoveryTimePeriod>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="ErikEJ.DacFX.SqlServer.Rules" Version="5.1.2">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

The template has no `CodeAnalysisRules` override: all three projects likewise use the default rules, with no rule exclusions or severity overrides. The database model properties above are fixture fixes, not template settings. The analyzer package is not a database dependency; the two producer dependencies remain direct DACPAC references.

- **Semantic model validation** resolves database/table/column references using both DACPACs before static analysis. `TreatTSqlWarningsAsErrors=True` promotes unresolved-reference warnings to errors. Removing either dependency fails with `error SQL71561` naming the missing producer table, before the analyzer starts.
- **Static lint** loads both built-in and ErikEJ rules. Successful execution logs `Using analyzers: Microsoft.Rules.Data, SqlServer.Rules` and `Successfully analyzed package`, and writes an empty `<Problems />` report for each project at `<Project>/obj/Release/net10.0/<Project>.CodeAnalysis.xml`.
- The first ErikEJ-enabled build caught SRD0078 (single-character aliases), SRD0700 (page verification), SRD0701 (Query Store state), SRD0703 (capture mode), and SRD0704 (recovery time). Fixed the SQL with descriptive singular aliases `account` and `customerOrder`, and set the model properties shown above. The tables/files are singular `Account.sql` / `dbo.Account` and `Order.sql` / `dbo.[Order]`; the view/file is `AccountOrder.sql` / `dbo.AccountOrder`. Brackets protect the reserved table name `Order`.
- Enabling the same analyzers on both producers caught SRN0007 (unnamed primary-key constraints). Named them `PK_Account` and `PK_Order`; no rule was suppressed. Both producers use the same database model properties shown above.
- Installing the template package resolves the SQLPROJ0001 missing-analyzer advisory. The commands and automated fixture builds now use blanket MSBuild `-warnaserror` as well as semantic warnings-as-errors. No `NoWarn`, `SuppressTSqlWarnings`, missing-dependency suppression, or rule suppression was introduced.

Verified with SDK 10.0.112 / DacFx 170.5.96.0: both producers and the valid/restored consumer succeed with zero warnings/errors; omitting either reference independently fails with SQL71561. Use `-t:Rebuild` to force analysis rather than reuse an incremental result.

The automated regression copies only the fixture SQL/project inputs into a unique per-invocation directory under `test/DacpacTool.Tests/obj/DirectReferences`, rewrites SDK imports to the local SDK, and removes the copy during cleanup. Its builds never write the checked-in fixture's `bin`/`obj`, so separate net8.0/net10.0 test hosts can run concurrently. The manual commands above still build in place. Both test hosts build the scratch projects as `net10.0` via SDK 10; the test host framework does not change the fixture target framework.

It verifies package/version/asset parity against the actual template for all three projects, checks that no rule override is present, then runs the three builds in order and requires each to load `SqlServer.Rules` and produce a clean analysis report. It verifies both copied references and the consumer view in the resulting DacFx model, checks both independent failure cases, then rebuilds the valid consumer:

```sh
dotnet test test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~DirectDacpacReferenceTests
dotnet test test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release -f net8.0 --filter FullyQualifiedName~DirectDacpacReferenceTests
```
