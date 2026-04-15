# ER Diagram Harness

This project is a regression harness for ER diagram generation.

It exists to verify:

- generated Mermaid markdown matches checked-in expected files
- filtered diagrams behave correctly with `schemas` and `tables`
- foreign keys can still point to filtered-out tables by rendering empty placeholder nodes
- odd schema cases, including computed-column foreign key patterns, continue to render consistently

## Files

- `ErDiagramHarnessProject.csproj`: SDK-backed test project that generates multiple ER diagrams during build
- `Configs/*_erdiagram.json`: diagram filter configurations
- `Expected/*.md`: golden files used for output comparison
- `Tables/*.sql`: schema objects used by the harness

Each config file can optionally reference the schema for editor help:

```json
{
  "$schema": "../../../src/MSBuild.Sdk.SqlProj/Sdk/EntityRelationshipDiagramConfig.schema.json"
}
```

## Run The Harness Tests

Run the project-harness tests:

```bash
dotnet test test/DacpacTool.Tests/DacpacTool.Tests.csproj --filter FullyQualifiedName~ErDiagramProjectHarnessTests
```

Run only the golden-file comparison test:

```bash
dotnet test test/DacpacTool.Tests/DacpacTool.Tests.csproj --filter FullyQualifiedName~ErDiagramProjectHarnessTests.BuildProject_GeneratesExpectedDiagrams
```

## Optional Mermaid CLI Validation

There is also an optional test that validates the generated Mermaid blocks can be rendered by Mermaid CLI.

Install Mermaid CLI so `mmdc` is available on your `PATH`:

- Install and usage docs: https://github.com/mermaid-js/mermaid-cli?tab=readme-ov-file
- Linux sandbox issue notes: https://github.com/mermaid-js/mermaid-cli/blob/master/docs/linux-sandbox-issue.md

Then run:

```bash
dotnet test test/DacpacTool.Tests/DacpacTool.Tests.csproj --filter FullyQualifiedName~ErDiagramProjectHarnessTests.BuildProject_GeneratedDiagramsParseWithMermaidCli_WhenAvailable
```

If `mmdc` is not installed, that test is skipped.

The test writes a temporary Puppeteer config and runs Mermaid CLI with `--no-sandbox`, based on the Mermaid CLI Linux sandbox guidance above.

At the moment, the Mermaid CLI validation test is expected to catch real Mermaid parse failures that the golden-file tests do not catch. For example, a computed column expression like the following currently causes Mermaid to fail parsing:

```text
Error: Error: Parse error on line 7:
...ENISNULL([IsBlue]-0)=1ORISNULL([IsRed]-0
-----------------------^
Expecting 'BLOCK_STOP', 'ATTRIBUTE_WORD', 'ATTRIBUTE_KEY', 'COMMENT', got '='
```
