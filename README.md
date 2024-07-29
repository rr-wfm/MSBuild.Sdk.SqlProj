# MSBuild.Sdk.SqlProj

![Build Status](https://github.com/jmezach/MSBuild.Sdk.SqlProj/workflows/CI/badge.svg)
![Latest Stable Release](https://img.shields.io/nuget/v/MSBuild.Sdk.SqlProj)
![Latest Prerelease](https://img.shields.io/nuget/vpre/MSBuild.Sdk.SqlProj)
![Downloads](https://img.shields.io/nuget/dt/MSBuild.Sdk.SqlProj)

## Introduction

A MSBuild SDK that is capable of producing a SQL Server Data-Tier Application package (.dacpac) from a set of SQL scripts that can be subsequently deployed using the `Microsoft.SqlPackage` [dotnet tool](https://www.nuget.org/packages/Microsoft.SqlPackage). It provides much of the same functionality as the SQL Server Data Tools .sqlproj project format, but is built on top of the new SDK-style projects that were first introduced in Visual Studio 2017.

If you're looking for a video introduction, please watch this [dotnetFlix episode](https://dotnetflix.com/player/104). For some more background on this project read the following blogposts:

- [Introducing MSBuild.Sdk.SqlProj](https://jmezach.github.io/post/introducing-msbuild-sdk-sqlproj/)
- [An update on MSBuild.Sdk.SqlProj](https://jmezach.github.io/post/update-on-msbuild-sdk-sqlproj/)

## Code of conduct
Please take a moment to familiarize yourself with the [code of conduct](CODE_OF_CONDUCT.md) for this repository.

## Usage
The simplest way to get started is to install our templates with `dotnet new` using:

```
dotnet new --install MSBuild.Sdk.SqlProj.Templates
```

You can then create a new project file using the following command:
```
dotnet new sqlproj
```

If you don't want to target the latest version of SQL Server, you can specify a version to target using the `-s Sql<version>` switch. 

See [How to determine the version, edition, and update level of SQL Server and its components](https://support.microsoft.com/help/321185/how-to-determine-the-version-edition-and-update-level-of-sql-server-an) to map from the SQL Server SKU name to the version number, e.g. `SQL Server 2016` to `13.0`.

See [SqlServerVersion Enum](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.sqlserverversion) to map from the SQL Server version number to the `SqlServerVersion` needed by the template and project file, e.g. `13.0` to `Sql130`.

```
dotnet new sqlproj -s Sql130
```

You should now have a project file with the following contents:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <SqlServerVersion>Sql130</SqlServerVersion>
        <!-- For additional properties that can be set here, please refer to https://github.com/rr-wfm/MSBuild.Sdk.SqlProj#model-properties -->
    </PropertyGroup>

    <PropertyGroup>
        <!-- Refer to https://github.com/rr-wfm/MSBuild.Sdk.SqlProj#publishing-support for supported publishing options -->
    </PropertyGroup>
</Project>
```

Then run a `dotnet build` and you'll find a .dacpac file with the same name as your project file in the `bin\Debug\netstandard2.0` folder. If you want to change the name of the `.dacpac` file you can set the `<TargetName>` property in your project file to something else.

> Note: For PackageReferences this SDK currently assumes that the `.dacpac` file has the same name as the package. If you plan to create a NuGet package out of your project (see [below](#packaging-support)) then make sure that `<TargetName>` matches the ID of your package.

> Note: Ensure you **do not** change the `TargetFramework` element from `netstandard2.0` to anything else. Otherwise, you will see errors like `The "CopyRefAssembly" task was not given a value for the required parameter "SourcePath"`.

## Editing the Project file
The project file can have extension `.csproj` or `.fsproj`.

All `.sql` files in the same directory as the project file, except for those in the `Pre-Deployment` and `Post-Deployment` folders, are added to the package by default.

To exclude files from the package, add a `<Content Remove="Directory\File.sql" />`.

If you are using Visual Studio, to make files excluded from the package appear in Solution Explorer (like scripts for Pre- and Post-Deployment), add `<None Include="Directory\File.sql" />`.

Wildcards are supported for all nodes (`Content`, `None`, etc.). For example, `<None Include="Directory\**" />`.

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

> Note: In a future update of Visual Studio, you should be able to use both the project template and the item templates directly from Visual Studio. This feature is currently in preview and some of our early testing has revealed that it doesn't work as expected. Stay tuned for updates on this.

If you already have a SSDT (.sqlproj) project in your solution, you can keep that as a "companion" project in order to enjoy the Visual Studio designer experience, as described in [this blog post](https://erikej.github.io/efcore/2020/05/11/ssdt-dacpac-netcore.html).

## Model properties
There are a lot of properties that can be set on the model in the resulting `.dacpac` file which can be influenced by setting those properties in the project file using the same name. For example, the snippet below sets the `RecoveryMode` property to `Simple`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <RecoveryMode>Simple</RecoveryMode>
        <SqlServerVersion>SqlAzure</SqlServerVersion>
    </PropertyGroup>
</Project>
```

Refer to the [documentation](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.tsqlmodeloptions) for more details on the available properties. The [SqlServerVersion](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.sqlserverversion) property is also  supported.

**Note:** If you are replacing an existing `.sqlproj` be sure to copy over any of these properties into the new project file.

## Model compiler options
Like `.sqlproj` projects  `MSBuild.Sdk.SqlProj` supports controlling T-SQL build errors and warnings by using MSBuild properties.
Treating warnings as errors can be optionally enabled by adding a property `TreatTSqlWarningsAsErrors` to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TreatTSqlWarningsAsErrors>True</TreatTSqlWarningsAsErrors>
        ...
    </PropertyGroup>
</Project>
```
> Note: Alternatively, you can use `TreatWarningsAsErrors` instead of `TreatTSqlWarningsAsErrors` to apply the same effect.

To suppress specific warnings from being treated as errors, add a comma-separated list of warning codes to `SuppressTSqlWarnings` property in the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <SuppressTSqlWarnings>71558,71502</SuppressTSqlWarnings>
        <TreatTSqlWarningsAsErrors>True</TreatTSqlWarningsAsErrors>
        ...
    </PropertyGroup>
</Project>
```

You can suppress warnings for a specific file by adding `SuppressTSqlWarnings` for this file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        ...
    </PropertyGroup>

    <ItemGroup>
        <Content Include="Procedures\csp_Test.sql">
            <SuppressTSqlWarnings>71502</SuppressTSqlWarnings>
        </Content>
    </ItemGroup>
</Project>
```
> Note: Warnings suppressed at the project level are always applied to every file in the project, regardless of what is configured at the file level.

## Pre- and post deployment scripts
Support for pre- and post-deployment scripts has been added in version 1.1.0. These scripts will be automatically executed when deploying the `.dacpac` to SQL Server.

To include these scripts into your `.dacpac` add the following to your `.csproj`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        ...
    </PropertyGroup>

  <ItemGroup>
    <PostDeploy Include="Post-Deployment\Script.PostDeployment.sql" />
    <PreDeploy Include="Pre-Deployment\Script.PreDeployment.sql" />
  </ItemGroup>
</Project>
```

It is important to note that scripts in the `Pre-Deployment` and `Post-Deployment` folders are excluded from the build process by default. This is because these scripts typically don't define database objects, such as tables and stored procedure, but perform other tasks that cannot be represented in the model. If these aren't excluded your build might break with a SQL46010 error. Instead, you should create a script file that includes all of those scripts using the `:r <path-to-script>.sql` syntax and then reference that script in your project file (as shown above).

By default the pre- and/or post-deployment script of referenced packages (both [PackageReference](#package-references) and [ProjectReference](#project-references)) are not run when using `dotnet publish`. As of version 1.11.0 this can be optionally enabled by adding a property `RunScriptsFromReferences` to the project file as in the below example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <RunScriptsFromReferences>True</RunScriptsFromReferences>
        ...
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="MyDatabasePackage" Version="1.0.0" />
    </ItemGroup>
</Project>
```

## SQLCMD variables
Especially when using pre- and post-deployment scripts, but also in other scenario's, it might be useful to define variables that can be controlled at deployment time. This is supported using SQLCMD variables, added in version 1.1.0. These variables can be defined in your project file using the following syntax:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        ...
    </PropertyGroup>

  <ItemGroup>
    <SqlCmdVariable Include="MySqlCmdVariable">
      <DefaultValue>DefaultValue</DefaultValue>
      <Value>$(SqlCmdVar__1)</Value>
    </SqlCmdVariable>
    <SqlCmdVariable Include="MySqlCmdVariable2">
      <DefaultValue>DefaultValue</DefaultValue>
      <Value>$(SqlCmdVar__2)</Value>
    </SqlCmdVariable>
  </ItemGroup>
</Project>
```

> Note: In versions prior to 1.11.0 the `DefaultValue` element displayed above was not used. As of version 1.11.0 the value of `Value` is checked first and if it found to be empty, we'll fall back to `DefaultValue`.

## Package references
`MSBuild.Sdk.SqlProj` supports referencing NuGet packages that contain `.dacpac` packages. These can be referenced by using the `PackageReference` format familiar to .NET developers. They can also be installed through the NuGet Package Manager in Visual Studio.

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" />
    </ItemGroup>
</Project>
```

It will assume that the `.dacpac` file is inside the `tools` folder of the referenced package and that it has the same name as the NuGet package. Referenced packages that do not adhere to this convention will be silently ignored. However, you have the ability to override this convention by using the `DacpacName` attribute on the `PackageReference` (introduced in version 2.5.0). For example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <SqlServerVersion>Sql160</SqlServerVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" DacpacName="SomeOtherDacpac" />
    </ItemGroup>
</Project>
```

This will add a reference to the `tools\SomeOtherDacpac.dacpac` file inside the `MyDatabasePackage` package. Note that if that file doesn't exist within the package, the package reference will still be silently ignored. However, the build will most likely fail if your project actually references objects from the reference package.

By default, the package reference is treated as being part of the same database. For example, if the reference package contains a `.dacpac` that has a table and a stored procedure and you would `dotnet publish` the project the table and stored procedure from that package will be deployed along with the contents of your project to the same database. If this is not desired, you can add the `DatabaseVariableLiteralValue` item metadata to the `PackageReference` specifying a different database name:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeOtherDatabase" />
    </ItemGroup>
</Project>
```

In this scenario you can access the objects defined by `MyDatabasePackage` by using the `[SomeOtherDatabase].[<schema>].[<object>]` syntax.

You can also use SQLCMD variables to set references, similar to the behavior of a `.sqlproj` project, by adding `DatabaseSqlCmdVariable` and optionally `ServerSqlCmdVariable` item metadata to the `PackageReference` element:
>Note: Don't forget to define appropriate [SQLCMD variables](#sqlcmd-variables)

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseSqlCmdVariable="SomeOtherDatabase" ServerSqlCmdVariable="SomeOtherServer"/>
    </ItemGroup>

  <ItemGroup>
    <SqlCmdVariable Include="SomeOtherDatabase">
      <DefaultValue>OtherDatabase</DefaultValue>
      <Value>$(SqlCmdVar__1)</Value>
    </SqlCmdVariable>
    <SqlCmdVariable Include="SomeOtherServer">
      <DefaultValue>OtherServer</DefaultValue>
      <Value>$(SqlCmdVar__2)</Value>
    </SqlCmdVariable>
  </ItemGroup>
</Project>
```
In this scenario you can access the objects defined by `MyDatabasePackage` by using the `[$(SomeOtherServer)].[$(SomeOtherDatabase)].[<schema>].[<object>]` syntax.
Also you can combine `ServerSqlCmdVariable` with `DatabaseVariableLiteralValue` and use  `[$(SomeOtherServer)].[SomeOtherDatabase].[<schema>].[<object>]` syntax.

When deploying a dacpac with references to other dacpacs, if you want the contents of all dacpacs to be deployed to a single database you will need to specify the `IncludeCompositeObjects` property. For example:

```bash
sqlpackage
    /Action:Publish \
    /SourceFile:MyDatabase.dacpac \
    /TargetServerName:localhost \
    /TargetDatabaseName:MyDatabase \
    /TargetUser:sa \
    /TargetPassword: MyP@ssword \
    /Properties:IncludeCompositeObjects=True
```

## Referencing system databases
Microsoft has recently released NuGet packages containing the definitions of the `master` and `msdb` databases. This is useful if you want to reference objects from those databases within your own projects without getting warnings. To reference these, you'll need to use at least version 2.5.0 of MSBuild.Sdk.SqlProj as you'll need to use the `DacpacName` feature for package references described above. For example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <SqlServerVersion>160</SqlServerVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.SqlServer.Dacpacs.Master" Version="160.2.1" DacpacName="master" />
    </ItemGroup>
</Project>
```

The above example references the `master` database from the [Microsoft.SqlServer.Dacpacs.Master](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master) NuGet package. Please note that there are different major versions of that package for different versions of SQL Server. It is recommended to reference the most recent minor/patch version of the package as the `SqlServerVersion` you are targeting with your project, as seen in the example above.

For other variants of SQL Server / Azure SQL Database there are dedicated packages as [listed here](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master#readme-body-tab).

## Project references
Similar to package references you can also reference another project by using a `ProjectReference`. These references can be added manually to the project file or they can be added through Visual Studio. For example, consider the following example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" />
    </ItemGroup>
</Project>
```

This will ensure that `MyOtherProject` is built first and the resulting `.dacpac` will be referenced by this project. This means you can use the objects defined in the other project within the scope of this project. If the other project is representing an entirely different database, you can also use `DatabaseVariableLiteralValue` or SQLCMD variables on the `ProjectReference` similar to `PackageReference`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" DatabaseVariableLiteralValue="SomeOtherDatabase" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="../MySecondProject/MySecondProject.csproj" DatabaseSqlCmdVariable="SecondOtherDatabase" ServerSqlCmdVariable="SomeOtherServer" />
    </ItemGroup>

  <ItemGroup>
    <SqlCmdVariable Include="SecondOtherDatabase">
      <DefaultValue>SecondDatabase</DefaultValue>
      <Value>$(SqlCmdVar__1)</Value>
    </SqlCmdVariable>
    <SqlCmdVariable Include="SomeOtherServer">
      <DefaultValue>OtherServer</DefaultValue>
      <Value>$(SqlCmdVar__2)</Value>
    </SqlCmdVariable>
  </ItemGroup>

</Project>
```

> Note: We do not support adding a `ProjectReference` to an existing `.sqlproj` file.


## Circular References and SuppressMissingDependenciesErrors
In order to solve circular references between databases that may have been incorrectly setup, it is possible to add
`SuppressMissingDependenciesErrors` to both [Package References](#package-references) and [ProjectReferences](#project-references)):

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeDatabase" SuppressMissingDependenciesErrors="True"/>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" DatabaseVariableLiteralValue="SomeOtherDatabase" SuppressMissingDependenciesErrors="True"/>
    </ItemGroup>
</Project>
```

## Packaging support

`MSBuild.Sdk.SqlProj` version 2.8.0 and later supports packaging your project into a [NuGet](https://www.nuget.org) package using the `dotnet pack` command.

You'll need to set the `PackageProjectUrl` property in the `.csproj` like this:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
  <PropertyGroup>
    ...
    <PackageProjectUrl>your-project-url</PackageProjectUrl>
  </PropertyGroup>

</Project>
```

Other metadata for the package can be controlled by using the [documented](https://docs.microsoft.com/dotnet/core/tools/csproj#nuget-metadata-properties) properties in your project file.

### Packaging standalone dacpacs

If you have an already-compiled `.dacpac` file without a corresponding `.csproj` that you need to reference as a `PackageReference`, you can use existing NuGet functionality to wrap the dacpac in a NuGet package. To do that, create a `.nuspec` file referencing your dacpac:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>your-dacpac-name</id>
    <version>your-version-number</version>
    <description>your-description</description>
    <authors>your-author</authors>
    <owners>your-owner</owners>
  </metadata>
  <files>
    <file src="fileName.dacpac" target="tools/" />
  </files>
</package>
```

To create the package, run: 
```
nuget pack fileName.nuspec
```

Then push the package to your local NuGet repository: 
```
nuget push fileName.version.nupkg -Source /your/nuget/repo/path
```

You can now reference your dacpac as a `PackageReference`!

> Note: To run these commands, you'll need to have the NuGet CLI tools installed. See [these installation instructions](https://docs.microsoft.com/nuget/install-nuget-client-tools#nugetexe-cli). If you use Chocolatey, you can also install by running `choco install nuget.commandline`. On a Mac with Homebrew installed, use `brew install nuget`.

## Publishing support
Starting with version 1.2.0 of MSBuild.Sdk.SqlProj there is support for publishing a project to a SQL Server using the `dotnet publish` command. This support is designed to be used by developers to deploy or update their local development database quickly. For more advanced deployment scenario's we suggest using [SqlPackage](https://docs.microsoft.com/en-us/sql/tools/sqlpackage/sqlpackage?view=sql-server-ver15) instead as it provides more options.

There are a couple of properties that control the deployment process which have some defaults to make the experience as smooth as possible for local development. For example, on Windows if you have a default SQL Server instance running on your local machine running `dotnet publish` creates a database with the same name as the project. Unfortunately on Mac and Linux we cannot use Windows authentication, so you'll need to specify a username and password:

```
dotnet publish /p:TargetUser=<username> /p:TargetPassword=<password>
```

To further customize the deployment process, you can use the following properties which can either be set in the project file or specified on the command line (using the `/p:<property>=<value>` syntax shown above).

| Property | Default Value | Description |
| --- | --- | --- |
| TargetServerName | (local) | Controls the name of the server to which the project is published |
| TargetDatabaseName | Project name | Controls the name of the database published by `dotnet publish` |
| TargetPort |  | Specifies an alternate port for connecting to the target server (only necessary if using a non-standard port) |
| TargetUser |  | Username used to connect to the server. If empty, Windows authentication is used |
| TargetPassword | | Password used to connect to the server. If empty, but TargetUser is set you will be prompted for the password |
| IncludeCompositeObjects | True | Controls whether objects from referenced packages are deployed to the same database |
| TargetName | Project name | Controls the name of the `.dacpac` created by `dotnet build`. The default name for the `.dacpac` file is the name of the project file, e.g. `MyProject.csproj` produces `MyProject.dacpac`. |
| DeployOnPublish | True | Controls whether a deploy occurs when the project is published. |

> IMPORTANT: Although you can set the username and password in your project file we don't recommend doing so since you'll be committing credentials to version control. Instead, you should specify these at the command line when needed.

In addition to these properties, you can also set any of the [documented](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacdeployoptions) deployment options. These are typically set in the project file, for example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
    <PropertyGroup>
        ...
        <BackupDatabaseBeforeChanges>True</BackupDatabaseBeforeChanges>
        <BlockOnPossibleDataLoss>True</BlockOnPossibleDataLoss>
        ...
    </PropertyGroup>
</Project>
```

Most of those properties are simple values (like booleans, strings and integers), but there are a couple of properties that require more complex values:

| Property | Example value | Description |
| --- | --- | --- |
| DatabaseSpecification | Hyperscale,1024,P15 | This property is specified in the format [Edition](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacazureedition),[Maximum Size](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacazuredatabasespecification.maximumsize),[Service Objective](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacazuredatabasespecification.serviceobjective) |
| DoNotDropObjectTypes | Aggregates,Assemblies | A comma separated list of [Object Types](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.objecttype) that should not be dropped as part of the deployment |
| ExcludeObjectTypes | Contracts,Endpoints | A comma separated list of [Object Types](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.objecttype) that should not be part of the deployment |
| SqlCommandVariableValues | | These should not be set as a Property, but instead as an ItemGroup as described [here](#SQLCMD-Variables)

## Script generation
Instead of using `dotnet publish` to deploy changes to a database, you can also have a full SQL script generated that will create the database from scratch and then run that script against a SQL Server. This can be achieved by adding the following to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
  <PropertyGroup>
      <GenerateCreateScript>True</GenerateCreateScript>
      <IncludeCompositeObjects>True</IncludeCompositeObjects>
  </PropertyGroup>
</Project>
```

With this enabled you'll find a SQL script with the name `<database-name>_Create.sql` in the bin folder.
The database name for the create script gets resolved in the following manner:
1. `TargetDatabaseName`.
1. Package name.
> Note: 
>- the generated script also uses the resolved database name via a setvar command.
>- if `IncludeCompositeObjects` is true, the composite objects (tables, etc.) from external references are also included in the generated script. This property defaults to `true`

## Static code analysis
Starting with version 2.7.0 of the SDK, there is support for running static code analysis during build. The SDK includes the following sets of rules:

- Microsoft.Rules ([1](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd193411(v=vs.100)), [2](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd193246(v=vs.100)) and [3](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd172117(v=vs.100)))
- [SqlServer.Rules](https://github.com/tcartwright/SqlServer.Rules/blob/master/docs/table_of_contents.md)
- [Smells](https://github.com/davebally/TSQL-Smells)

Static code analysis can be enabled by adding the `RunSqlCodeAnalysis` property to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
  <PropertyGroup>
    <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
    <CodeAnalysisRules>-SqlServer.Rules.SRD0006;-Smells.*</CodeAnalysisRules>
  </PropertyGroup>
</Project>
```

A xml file with the analysis results is created in the output folder.

The optional `CodeAnalysisRules` property allows you to disable individual rules or groups of rules.

Any rule violations found during analysis are reported as build warnings.

Individual rule violations or groups of rules can be configured to be reported as build errors as shown below.

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/2.8.0">
  <PropertyGroup>
    <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
    <CodeAnalysisRules>+!SqlServer.Rules.SRN0005;+!SqlServer.Rules.SRD*</CodeAnalysisRules>
  </PropertyGroup>
</Project>
```

You can also bring your own rules. For an example of custom rules, see [this repository](https://github.com/ErikEJ/SqlServer.Rules).

To use custom rules, place the rule .dll files in a `Rules` folder in the project, and add them as Content items:

```xml
  <ItemGroup>
    <Content Include="Rules\My.Own.Rules.dll" />
  </ItemGroup>
```

## Integrations

`MSBuild.Sdk.SqlProj` integrates with a number of other technologies and tools. Here are some examples:

- [MSBuild.Sdk.SqlProj.Aspire](https://www.nuget.org/packages/MSBuild.Sdk.SqlProj.Aspire) - Library that provides .NET Aspire integration for MSBuild.Sdk.SqlPproj projects. It allows you to publish SQL Database Projects as part of your .NET Aspire AppHost projects.

- [dotnet-sqltest](https://github.com/cagrin/dotnet-sqltest) - Command line tool for running tSQLt unit tests from MSBuild.Sdk.SqlProj projects.

- [EF Core Power Tools](https://marketplace.visualstudio.com/items?itemName=ErikEJ.EFCorePowerTools&ssr=false#overview) - Visual Studio extension that can generate an Entity Framework Core DbContext and model classes directly from your `MSBuild.Sdk.SqlProj` project.

## Workaround for parser errors (SQL46010)
This project relies on the publicly available T-SQL parser which may not support all T-SQL syntax constructions. Therefore, you might encounter a SQL46010 error if you have a script file that contains unsupported syntax. If that happens, there's a couple of workarounds you can try:

1. Exclude the file from the build entirely by changing its build action to None.
1. Move the offending script to the pre- and/or post-deployment scripts.
1. Use dynamic SQL instead, like this:

```sql
DECLARE @Query NVARCHAR(MAX) = '<your-script>'
EXEC (@Query)
```

## Reference `MSBuild.Sdk.SqlProj` from class library
The output of `MSBuild.Sdk.SqlProj` is not an assembly, but a `.dacpac`. In order to correctly reference a `MSBuild.Sdk.SqlProj` based project from a class library, the `ReferenceOutputAssembly` hint needs to be set to `False`:
```
<ItemGroup>
    <ProjectReference
      Include="../MyDacpacProj/MyDacpacProj.csproj"
      ReferenceOutputAssembly="False" />
</ItemGroup>
```
Now, upon compilation of the class library, the relevant `.dacpac` files get copied to the output directory.

## Known limitations
Since this is not an entire project system but only an MSBuild SDK we cannot provide IntelliSense for objects defined within the project. This limitation can be circumvented by connecting the SQL editor to a live database that is used for development purposes.
