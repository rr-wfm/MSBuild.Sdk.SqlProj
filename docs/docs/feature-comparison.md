# Feature Comparison

## Other approaches

Microsoft provides two other project types, that have features similar to this SDK.

- "Classic" SSDT projects in Visual Studio - only builds and works on Windows, and supports multiple designers.
- Microsoft.Build.Sql based projects - SDK-style projects, that work on all platforms, and are supported by tooling in Visual Studio Code (VS Code) and SQL Server Management Studio (SSMS) - no Visual Studio support at the moment.

Microsoft has published [this comparison table](https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/sql-projects-tools?view=sql-server-ver17#feature-set-comparison), and based on that, we have added this SDK used in combination with the SQL Database Projects Power Tools extension for Visual Studio.

### Feature set comparison

| Feature | VS Code (Microsoft.Build.Sql) | SSMS (Microsoft.Build.Sql) | VS (Classic .sqlproj) | VS (This SDK with Power Tools) |
| --- | --- | --- | --- | --- |
| Create new empty project | Yes | Yes | Yes | Yes |
| Create new project from existing database | Yes | Yes | Yes | Yes |
| Solution management and operations | No | Yes | Yes | Yes |
| Project run build | Yes | Yes | Yes | Yes |
| Cross platform build | Yes | Yes | No | Yes |
| Publish project to existing server | Yes | Yes | Yes | Yes |
| Publish project to a local development instance | Yes<sup>1</sup> | Yes<sup>2</sup> | Yes<sup>3</sup> | Yes<sup>3</sup> |
| Publish options/properties | Yes | Yes | Yes | Yes (in .csproj) |
| Target platform can be updated | Yes | Yes | Yes | Yes |
| SQLCMD variables | Yes | Yes | Yes | Yes |
| Project references | Yes | Yes | Yes | Yes |
| DACPAC references | Yes | Yes | Yes | No |
| Package references | Yes | Yes | No | Yes |
| Analyzer package references | Yes | Yes | No | Yes |
| Publish profile creation | Yes | Yes | Yes | Yes |
| SQL files can be added by placing in project folder | Yes | Yes | No | Yes |
| SQL files can be excluded from build | Yes | Yes | Yes | Yes |
| Pre-deployment and post-deployment scripts | Yes | Yes | Yes | Yes |
| New object templates | Yes | Yes | Yes | Yes<sup>4</sup> |
| Project files can be organized into folders | Yes | Yes | Yes | Yes |
| Schema comparison project to database | Yes | Yes | Yes | Yes |
| Schema comparison database to project | Yes | Yes | Yes | Yes |
| Graphical table designer | No | No | Yes | No |
| Code analysis - enable/disable rules GUI | Yes | Yes | Yes | Yes |
| Code analysis - extensive GUI with doc links and more | No | No | No | Yes |
| Project properties - build output settings | No | No | Yes | No |
| Project properties - database settings GUI | No | No | Yes | No |
| Project run code analysis | Yes | Yes | Yes | Yes |
| Object renaming and refactoring | Yes | No | Yes | No |
| Intellisense provided in database files from project model | Yes | No | Yes | No |
| Generate Create script during build | No | No | No | Yes |
| Generate E-R diagrams during build | No | No | No | Yes |

1. Local development instance is a SQL Server container.
2. Any pre-installed Microsoft SQL database can be used as a local development instance.
3. Local development instance is a SQL Server LocalDB instance.
4. Limited subset of templates available

## IDE support

The table below shows which IDEs support each of the three project types.

| IDE | Classic .sqlproj | Microsoft.Build.Sql .sqlproj | MSBuild.Sdk.SqlProj |
| --- | --- | --- | --- |
| Visual Studio | Yes | No | Yes (with [SQL Database Projects Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.SqlDatabaseProjectsPowerTools) extension) |
| VS Code | No | Yes (with [SQL Database Projects](https://marketplace.visualstudio.com/items?itemName=ms-mssql.sql-database-projects-vscode) extension) | Partial<sup>5</sup> |
| SSMS | No | Yes | No |
| Rider | No | Yes | Partial<sup>5</sup> |

5. Can open and build MSBuild.Sdk.SqlProj projects as standard .NET SDK projects, but there is no dedicated SQL tooling or IntelliSense for the `.sql` files.
