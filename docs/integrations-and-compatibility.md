---
id: integrations-and-compatibility
title: Integrations and Compatibility
---

# Integrations and Compatibility

## Integrations

`MSBuild.Sdk.SqlProj` integrates with a number of other technologies and tools. Here are some examples:

- [CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects) - Library that provides .NET Aspire integration for MSBuild.Sdk.SqlProj and Microsoft.Sql.Build projects. It allows you to publish SQL Database Projects as part of your .NET Aspire AppHost projects.

- [dotnet-sqltest](https://github.com/cagrin/dotnet-sqltest) - Command line tool for running tSQLt unit tests from MSBuild.Sdk.SqlProj projects.

- [SQL Project Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlProjectPowerTools&ssr=false#overview) - Visual Studio extension that enhances the developer experience for MSBuild.Sdk.SqlProj projects with project and item templates, import of existing databases, E/R diagrams, static analysis reporting and more.

- [T-SQL Analyzer](https://marketplace.visualstudio.com/items?itemName=ErikEJ.TSqlAnalyzer) - Visual Studio extension that adds live best practices analysis to your SQL scripts in your MSBuild.Sdk.SqlProj project.

- [EF Core Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.EFCorePowerTools&ssr=false#overview) - Visual Studio extension that can generate an Entity Framework Core DbContext and model classes, and various diagrams directly from your MSBuild.Sdk.SqlProj project.

## .NET runtime support

MSBuild.Sdk.SqlProj requires a .NET SDK to be installed. Depending on the version of MSBuild.Sdk.SqlProj you can use varying versions of the .NET SDK (released once a year). The following table lists the supported .NET SDK versions by each MSBuild.Sdk.SqlProj version:

| MSBuild.Sdk.SqlProj version | .NET runtime version |
| ------------ | -------------------- |
| 3.2.0  | 8.0, 9.0, 10.0  |
| 3.0.0  | 8.0, 9.0  |
| 2.9.0  | 6.0, 8.0  |
| 2.6.0  | 6.0, 7.0, 8.0  |
| 2.2.0  | 3.1, 6.0, 7.0  |
| 2.0.0  | 3.1, 5.0, 6.0  |

> If you have support issues, be prepared for us to ask you to use the latest released version of MSBuild.Sdk.SqlProj.

## Workaround for parser errors (SQL46010)

This project relies on the publicly available T-SQL parser which may not support all T-SQL syntax constructions. Therefore, you might encounter a SQL46010 error if you have a script file that contains unsupported syntax. If that happens, there's a couple of workarounds you can try:

1. Exclude the file from the build entirely by changing its build action to None.
1. Move the offending script to the pre- and/or post-deployment scripts.
1. Use dynamic SQL instead, like this:

```sql
DECLARE @Query NVARCHAR(MAX) = '<your-script>'
EXEC (@Query)
```

## Known limitations

Since this is not an entire project system but only an MSBuild SDK we cannot provide IntelliSense for objects defined within the project. This limitation can be circumvented by connecting the SQL editor to a live database that is used for development purposes.
