# Project Configuration

## Model properties

There are a lot of properties that can be set on the model in the resulting `.dacpac` file which can be influenced by setting those properties in the project file using the same name. For example, the snippet below sets the `RecoveryMode` property to `Simple`:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RecoveryMode>Simple</RecoveryMode>
    <SqlServerVersion>SqlAzure</SqlServerVersion>
  </PropertyGroup>
</Project>
```

Refer to the [documentation](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.tsqlmodeloptions) for more details on the available properties. The [SqlServerVersion](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.model.sqlserverversion) property is also  supported.

> [!NOTE]
> If you are replacing an existing `.sqlproj` be sure to copy over any of these properties into the new project file.

## Model compiler options

Like `.sqlproj` projects  `MSBuild.Sdk.SqlProj` supports controlling T-SQL build errors and warnings by using MSBuild properties.

### Treat warnings as errors

Treating warnings as errors can be optionally enabled by adding a property `TreatTSqlWarningsAsErrors` to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <TreatTSqlWarningsAsErrors>True</TreatTSqlWarningsAsErrors>
    ...
  </PropertyGroup>
</Project>
```

> [!NOTE]
> Alternatively, you can use `TreatWarningsAsErrors` instead of `TreatTSqlWarningsAsErrors` to apply the same effect.

### Suppress warnings at the project level

To suppress specific warnings from being treated as errors, add a comma-separated list of warning codes to `SuppressTSqlWarnings` property in the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <SuppressTSqlWarnings>71558,71502</SuppressTSqlWarnings>
    <TreatTSqlWarningsAsErrors>True</TreatTSqlWarningsAsErrors>
    ...
  </PropertyGroup>
</Project>
```

### Suppress warnings for a specific file

You can suppress warnings for a specific file by adding `SuppressTSqlWarnings` for this file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
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

> [!NOTE]
> Warnings suppressed at the project level are always applied to every file in the project, regardless of what is configured at the file level.

## Pre-deployment and post-deployment scripts

[These scripts](https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/pre-post-deployment-scripts) will be automatically executed when deploying the `.dacpac` to SQL Server.

To include these scripts into your `.dacpac` add the following to your `.csproj`:

### Include deployment scripts

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
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

### Why these scripts are excluded from build

By default the pre- and/or post-deployment script of referenced packages (both [Package References](references.md#package-references) and [Project References](references.md#project-references)) are not run when using `dotnet publish`. This can be optionally enabled by adding a property `RunScriptsFromReferences` to the project file as in the below example:

### Run scripts from referenced packages

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
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

Especially when using pre-deployment and post-deployment scripts, but also in other scenarios, it might be useful to define variables that can be controlled at deployment time. This is supported using SQLCMD variables. These variables can be defined in your project file using the following syntax:

### Define SQLCMD variables

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
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

> [!NOTE]
> With version 3.0.0 of the SDK, the `DefaultValue` is not applied to the build output, in line with the standard `.sqlproj` behaviour.
