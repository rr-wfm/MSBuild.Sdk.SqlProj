# MSBuild.Sdk.SqlProj

![Build Status](https://github.com/jmezach/MSBuild.Sdk.SqlProj/workflows/CI/badge.svg)
![Latest Stable Release](https://img.shields.io/nuget/v/MSBuild.Sdk.SqlProj)
![Latest Prerelease](https://img.shields.io/nuget/vpre/MSBuild.Sdk.SqlProj)

## Introduction

An MSBuild SDK that is capable of producing a SQL Server Data-Tier Application package (.dacpac) from a set of SQL scripts that can be subsequently deployed using SqlPackage.exe. It provides much of the same functionality as the SQL Server Data Tools .sqlproj project format, but is build on top of the new SDK-style projects that were first introduced in Visual Studio 2017.

## Usage
The simplest usage is to create a new project file with the following contents:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>
</Project>
```

Then run a `dotnet build` and you'll find a .dacpac file in the `bin\Debug\netstandard2.0` folder. By default all `.sql` files will be added to the package, except for those in the `Pre-Deployment` and `Post-Deployment` folders.

## Model properties
There are a lot of properties that can be set on the model in the resulting `.dacpac` file which can be influenced by setting those properties in the project file using the same name. For example, the snippet below sets the `RecoveryMode` property to `Simple`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <RecoveryMode>Simple</RecoveryMode>
        <SqlServerVersion>SqlAzure</SqlServerVersion>
    </PropertyGroup>
</Project>
```

Refer to the [documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.dac.model.tsqlmodeloptions?view=sql-dacfx-140.3881.1) for more details on the available properties. The [SqlServerVersion](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.sqlserverversion) property is also  supported.

**Note:** If you are replacing an existing `.sqlproj` be sure to copy over any of these properties into the new project file.

## Pre- and post deployment scripts
Support for pre- and post deployment scripts has been added in version 1.1.0. These scripts will be automatically executed when deploying the `.dacpac` to SQL Server.

> LIMITATION: Currently there is no support for including other scripts from the pre- or post deployment script using the `:r OtherScript.sql` syntax. See [this issue](issues/23) for more details.

To include these scripts into your `.dacpac` add the following to your `.csproj`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
    <PropertyGroup>
        ...
    </PropertyGroup>

  <ItemGroup>
    <PostDeploy Include="Post-Deployment\Script.PostDeployment.sql" />
    <PreDeploy Include="Pre-Deployment\Script.PreDeployment.sql" />
  </ItemGroup>
</Project>
```

## SQLCMD variables
Especially when using pre- and post deployment scripts, but also in other scenario's, it might be useful to define variables that can be controlled at deployment time. This is supported through the use of SQLCMD variables, added in version 1.1.0. These variables can be defined in your project file using the following syntax:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
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

## Package references
`MSBuild.Sdk.SqlProj` supports referencing NuGet packages that contain `.dacpac` packages. These can be referenced by using the `PackageReference` format familiar to .NET developers. They can also be installed through the NuGet Package Manager in Visual Studio.

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.1.0" />
    </ItemGroup>
</Project>
```

It will assume that the `.dacpac` file is inside the `tools` folder of the referenced package and that it has the same name as the NuGet package. Referenced packages that do not adhere to this convention will be silently ignored.

## Packaging support
`MSBuild.Sdk.SqlProj` supports packaging your project into a [NuGet](https://www.nuget.org) package using the `dotnet pack` command. In order for this to work, you'll need to add a `.nuspec` file next to your project file with the same name. For example, if your `.csproj` is called `TestProject.csproj` you'll need to add a `TestProject.nuspec` file in the same folder. Fill this file with the following contents and replace the placeholder with the appropriate value:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>$id$</id>
    <version>$version$</version>
    <description>$description$</description>
    <authors>$authors$</authors>
    <owners>$authors$</owners>
    <copyright>$copyright$</copyright>
    <projectUrl>$projecturl$</projectUrl>
    <tags>$tags$</tags>
    <packageTypes>
      <packageType name="$packagetype$" />
    </packageTypes>
    <repository type="git" url="<repository-url>" />
  </metadata>
  <files>
    <file src="bin\$configuration$\$tfm$\*.dacpac" target="tools/" />
  </files>
</package>
```

> Note: The above `.nuspec` file will result in a package that includes the .dacpac of the project as well as any referenced .dacpac file's (those from `PackageReference`). This is by design since you'll probably need those later on when deploying the .dacpac. If you want to only include the .dacpac of the project replace `*.dacpac` with `$id$.dacpac`.

Additionally you'll need to set the `PackageProjectUrl` property inside of the `.csproj` like this:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.1.0">
  <PropertyGroup>
    ...
    <PackageProjectUrl>your-project-url</PackageProjectUrl>
  </PropertyGroup>
</Project>
```

Other metadata for the package can be controlled by using the [documented](https://docs.microsoft.com/en-us/dotnet/core/tools/csproj#nuget-metadata-properties) properties in your project file.

## Workaround for parser errors (SQL46010)
This project relies on the publicly available T-SQL parser which may not support all T-SQL syntax constructions. Therefore you might encounter a SQL46010 error if you have a script file that contains unsupported syntax. If that happens, there's a couple of workarounds you can try:

1. Exclude the file from the build entirely by changing its build action to None.
1. Move the offending script to the pre- and/or post-deployment scripts.
1. Use dynamic SQL instead, like this:

```sql
DECLARE @Query NVARCHAR(MAX) = '<your-script>'
EXEC (@Query)
```

## Known limitations
Since this is not an entire project system but only an MSBuild SDK we cannot provide IntelliSense for objects defined within the project. This limitation can be circumvented by connecting the SQL editor to a live database that is used for development purposes.

The SQL Server Data Tools also includes a static code analysis feature. Currently this is not (yet) available when using this SDK.

Project references, that is references between project within the same solution, are also currently not (yet) supported.
