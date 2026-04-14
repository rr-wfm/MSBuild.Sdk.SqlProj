# Quick Start

## Usage

The simplest way to get started is to install our templates with `dotnet new` using:

```bash
dotnet new install MSBuild.Sdk.SqlProj.Templates
```

You can then create a new project file using the following command:

```bash
dotnet new sqlproj
```

If you don't want to target the latest version of SQL Server, you can specify a version to target using the `-s Sql<version>` switch.

See [How to determine the version, edition, and update level of SQL Server and its components](https://support.microsoft.com/help/321185/how-to-determine-the-version-edition-and-update-level-of-sql-server-an) to map from the SQL Server SKU name to the version number, e.g. `SQL Server 2022` to `16.0`.

See [SqlServerVersion Enum](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.sqlserverversion) to map from the SQL Server version number to the `SqlServerVersion` needed by the template and project file, e.g. `16.0` to `Sql160`.

```bash
dotnet new sqlproj -s Sql160
```

The project template also supports additional options such as enabling code analysis and ER diagram generation:

```bash
dotnet new sqlproj --codeAnalysis true --erDiagram true
```

When `--erDiagram true` is specified, the template includes a sample `*_erdiagram.json` config file and wires it into the generated project so you can customize filtered diagram output immediately.

You should now have a project file with the following contents:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <SqlServerVersion>Sql160</SqlServerVersion>
    <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
    <!-- For additional properties that can be set here, please refer to https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/project-configuration.html#model-properties -->
  </PropertyGroup>

  <ItemGroup>
    <!-- These packages adds additional code analysis rules -->
    <!-- We recommend using these, but they can be removed if desired -->
    <PackageReference Include="ErikEJ.DacFX.SqlServer.Rules" Version="3.2.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="ErikEJ.DacFX.TSQLSmellSCA" Version="3.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <PropertyGroup>
    <!-- Refer to https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/publishing.html for supported publishing options -->
  </PropertyGroup>
</Project>
```

Then run a `dotnet build` and you'll find a .dacpac file with the same name as your project file in the `bin\Debug\net10.0` folder. If you want to change the name of the `.dacpac` file you can set the `<TargetName>` property in your project file to something else.

> [!NOTE]
> For PackageReferences this SDK currently assumes that the `.dacpac` file has the same name as the package. If you plan to create a NuGet package out of your project (see [Packaging](packaging.md)) then make sure that `<TargetName>` matches the ID of your package.

## Editing the Project file

The project file can have extension `.csproj` or `.fsproj`.

All `.sql` files in the same directory as the project file, except for those in the `Pre-Deployment` and `Post-Deployment` folders, are added to the package by default.

To exclude files from the package, add a `<Content Remove="Directory\File.sql" />`.

If you are using Visual Studio, to make files excluded from the package appear in Solution Explorer (like scripts for Pre-Deployment and Post-Deployment), add `<None Include="Directory\File.sql" />`.

Wildcards are supported for all nodes (`Content`, `None`, etc.). For example, `<None Include="Directory\**" />`.

## Use an existing database

If you want to initialize your project with scripted objects from an existing database, you can do so with the following command:

```bash
sqlpackage \
  /Action:Extract \
  /Properties:ExtractTarget=Flat \
  /SourceConnectionString:"<connection_string>" \
  /TargetFile:<target_new_folder>
```

For example:

```bash
sqlpackage \
  /a:Extract \
  /p:ExtractTarget=Flat \
  /scs:"data source=.\SQLEXPRESS;initial catalog=Chinook;Trusted_Connection=true;encrypt=False" \
  /tf:.\Tables
```

> [!NOTE]
> The /tf parameter is currently required, and must refer to a non-existing folder.

You can read more about the `sqlpackage /a:Extract` command syntax [on Microsoft Learn](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-extract).

## Item templates

To create database objects you can use the following item templates:

| Template | Command | Description |
| --- | --- | --- |
| table | `dotnet new table -n <name> [-s <schema-name>]` | Creates a new database table with the provided name |
| view | `dotnet new view -n <name> [-s <schema-name>]` | Creates a new database view with the provided name |
| sproc | `dotnet new sproc -n <name> [-s <schema-name>]` | Creates a new stored procedure with the provided name |
| inlinefunc | `dotnet new inlinefunc -n <name> [-s <schema-name>]` | Creates a new inline function with the provided name |
| tablefunc | `dotnet new tablefunc -n <name> [-s <schema-name>]` | Creates a new table-valued function with the provided name |
| scalarfunc | `dotnet new scalarfunc -n <name> [-s <schema-name>]` | Creates a new scalar function with the provided name |
| uddt | `dotnet new uddt -n <name> [-s <schema-name>]` | Creates a new user-defined data type with the provided name |
| udtt | `dotnet new udtt -n <name> [-s <schema-name>]` | Creates a new user-defined table type with the provided name |

> [!NOTE]
> You can use both the project template and the item templates directly from Visual Studio, via the `File - New Project` and `Add New Item` dialogs.

If you already have a SSDT (.sqlproj) project in your solution, you can keep that as a "companion" project in order to enjoy the Visual Studio designer experience, as described in [this blog post](https://erikej.github.io/efcore/2020/05/11/ssdt-dacpac-netcore.html).
