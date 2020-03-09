# MSBuild.Sdk.SqlProj
**THis is currently a prototype and work-in-progress**
![Build Status](https://github.com/jmezach/MSBuild.Sdk.SqlProj/workflows/main/badge.svg)

## Introduction

An MSBuild SDK that is capable of producing a SQL Server Data-Tier Application package (.dacpac) from a set of SQL scripts that can be subsequently deployed using SqlPackage.exe. It provides much of the same functionality as the SQL Server Data Tools .sqlproj project format, but is build on top of the new SDK-style projects that were first introduced in Visual Studio 2017.

## Usage
The simplest usage is to create a new project file with the following contents:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.0.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    <PropertyGroup>
</Project>
```

Then run a `dotnet build` and you'll find a .dacpac file in the `bin\Debug\netstandard2.0` folder. By default all `.sql` files will be added to the package, except for those in the `Pre-Deployment` and `Post-Deployment` folders.

## Model properties
There are a lot of properties that can be set on the model in the resulting `.dacpac` file which can be influenced by setting those properties in the project file using the same name. For example, the snippet below sets the `RecoveryMode` property to `Simple`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.0.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <RecoveryMode>Simple</RecoveryMode>
    <PropertyGroup>
</Project>
```

Refer to the [documentation](https://docs.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.dac.model.tsqlmodeloptions?view=sql-dacfx-140.3881.1) for more details on the available properties.

**Note:** If you are replacing an existing `.sqlproj` be sure to copy over any of these properties into the new project file.

## Package references
`MSBuild.Sdk.SqlProj` supports referencing NuGet packages that contain `.dacpac` packages. These can be referenced by using the `PackageReference` format familiar to .NET developers. They can also be installed through the NuGet Package Manager in Visual Studio.

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/1.0.0">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
    <PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MyDatabasePackage" Version="1.0.0" />
    </ItemGroup>
</Project>
```

It will assume that the `.dacpac` file is inside the `tools` folder of the referenced package and that it has the same name as the NuGet package. Referenced packages that do not adhere to this convention will be silently ignored.

## Known limitations
Since this is not an entire project system but only an MSBuild SDK we cannot provide IntelliSense for objects defined within the project. This limitation can be circumvented by connecting the SQL editor to a live database that is used for development purposes.

Additionally the static code analysis features of SQL Server Data Tools aren't available yet.