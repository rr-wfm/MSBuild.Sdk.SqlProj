---
id: msbuild-sdk-sqlproj-vs-microsoft-build-sql
title: MSBuild.Sdk.SqlProj vs Microsoft.Build.Sql
---

# MSBuild.Sdk.SqlProj vs Microsoft.Build.Sql

## Should I still use `MSBuild.Sdk.SqlProj`?

Yes. `MSBuild.Sdk.SqlProj` is still actively used and maintained.

This project originally existed to make SQL Server database projects buildable outside Windows, long before `Microsoft.Build.Sql` existed. Now that both SDKs exist, the current maintainer view is that they can continue to coexist.

At the moment, there are no plans to abandon `MSBuild.Sdk.SqlProj`. The project is still used internally on a daily basis and continues to see significant community adoption.

## Is there a feature matrix?

There is currently no complete side-by-side feature matrix published for the two SDKs.

## Why choose `MSBuild.Sdk.SqlProj`?

Depending on your workflow, `MSBuild.Sdk.SqlProj` may still be the better fit if you need:

- [Static code analysis](/project-file#static-code-analysis) during build
- [Direct database publish support](/packaging-and-publishing#publishing-directly-to-sql-server) from the project
- [Container-image publishing](/packaging-and-publishing#publishing-as-a-container-image) from the project
- [ER diagram generation](/project-file#entity-relationship-diagram) during build
- [Package references](/references#package-references) to custom analyzer rules
- [Integrations](/integrations-and-compatibility) in the surrounding ecosystem

Documented integrations in this repository include:

- [.NET Aspire](https://github.com/CommunityToolkit/Aspire) integration through `CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects`
- [dotnet-sqltest](https://github.com/cagrin/dotnet-sqltest) tSQLt test execution
- [SQL Project Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools&ssr=false#overview) for project and item templates, import of existing databases, schema compare, analyzer rule management, and related Visual Studio tooling
- [TSqlAnalyzer](https://marketplace.visualstudio.com/items?itemName=ErikEJ.TSqlAnalyzer) live SQL best-practices analysis through T-SQL Analyzer
- [SQL Database Project Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools) EF Core model and diagram generation

For integrations, see the [Integrations and Compatibility](/integrations-and-compatibility) page.

## Why this SDK can still be the better choice

`Microsoft.Build.Sql` is evolving quickly, but its recent release notes are largely focused on core SQL project and DacFx capabilities such as target platform support, build output behavior, incremental build fixes, dependency updates, and additional database option support.

Those improvements are important, but they do not automatically replace the broader workflow value that some teams get from `MSBuild.Sdk.SqlProj`.

`Microsoft.Build.Sql` may cover the core build story for many projects, while `MSBuild.Sdk.SqlProj` can still be stronger in some higher-level developer workflow scenarios.

## Should I migrate to `Microsoft.Build.Sql`?

That depends on what you need from your SQL project workflow.

If `Microsoft.Build.Sql` already covers your build, tooling, and integration requirements, it may be a reasonable option. But there is currently no published migration guide from this project, and maintainers do not see an immediate need to migrate away from `MSBuild.Sdk.SqlProj`.

If the capabilities listed above are important to you, there is no reason to avoid `MSBuild.Sdk.SqlProj`.

## Current maintainer position

In short:

- both projects can coexist
- `MSBuild.Sdk.SqlProj` remains actively used
- some scenarios are still better served by `MSBuild.Sdk.SqlProj`
- migration to `Microsoft.Build.Sql` is not a current priority

This guidance is based on maintainer discussion from October 2025 within [issue 803](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues/803) and may evolve as both projects continue to develop.