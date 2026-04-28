# References

## Package references

`MSBuild.Sdk.SqlProj` supports referencing NuGet packages that contain `.dacpac` packages. These can be referenced by using the `PackageReference` format familiar to .NET developers. They can also be installed through the NuGet Package Manager in Visual Studio.

### Basic package references

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" />
  </ItemGroup>
</Project>
```

It will assume that the `.dacpac` file is inside the `tools` folder of the referenced package and that it has the same name as the NuGet package. Referenced packages that do not adhere to this convention will be silently ignored. However, you have the ability to override this convention by using the `DacpacName` attribute on the `PackageReference` (introduced in version 2.5.0). For example:

### Override the dacpac name

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DacpacName="SomeOtherDacpac" />
  </ItemGroup>
</Project>
```

This will add a reference to the `tools\SomeOtherDacpac.dacpac` file inside the `MyDatabasePackage` package. Note that if that file doesn't exist within the package, the package reference will still be silently ignored. However, the build will most likely fail if your project actually references objects from the reference package.

By default, the package reference is treated as being part of the same database. For example, if the reference package contains a `.dacpac` that has a table and a stored procedure and you would `dotnet publish` the project the table and stored procedure from that package will be deployed along with the contents of your project to the same database. If this is not desired, you can add the `DatabaseVariableLiteralValue` item metadata to the `PackageReference` specifying a different database name:

### Reference another database

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeOtherDatabase" />
  </ItemGroup>
</Project>
```

In this scenario you can access the objects defined by `MyDatabasePackage` by using the `[SomeOtherDatabase].[<schema>].[<object>]` syntax.

You can also use SQLCMD variables to set references, similar to the behavior of a `.sqlproj` project, by adding `DatabaseSqlCmdVariable` and optionally `ServerSqlCmdVariable` item metadata to the `PackageReference` element:

> [!NOTE]
> Don't forget to define appropriate [SQLCMD variables](project-configuration.md#sqlcmd-variables)

### Use SQLCMD variables

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
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

### Deploy composite objects

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

## Project references

Similar to package references you can also reference another project by using a `ProjectReference`. These references can be added manually to the project file or they can be added through Visual Studio. For example, consider the following example:

### Basic project references

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" />
  </ItemGroup>
</Project>
```

This will ensure that `MyOtherProject` is built first and the resulting `.dacpac` will be referenced by this project. This means you can use the objects defined in the other project within the scope of this project. If the other project is representing an entirely different database, you can also use `DatabaseVariableLiteralValue` or SQLCMD variables on the `ProjectReference` similar to `PackageReference`:

### Reference another database

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
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

> [!NOTE]
> We do not support adding a `ProjectReference` to an existing `.sqlproj` file.

## Referencing system databases

Microsoft has released NuGet packages containing the definitions of the `master` and `msdb` databases. This is useful if you want to reference objects from those databases within your own projects without getting warnings. To reference these, you'll need to use at least version 2.5.0 of MSBuild.Sdk.SqlProj as you'll need to use the `DacpacName` feature for package references described above. For example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SqlServer.Dacpacs.Master" Version="160.2.2" DacpacName="master" DatabaseVariableLiteralValue="master" />
  </ItemGroup>
</Project>
```

The above example references the `master` database from the [Microsoft.SqlServer.Dacpacs.Master](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master) NuGet package. Please note that there are different major versions of that package for different versions of SQL Server. It is recommended to reference the most recent minor/patch version of the package as the `SqlServerVersion` you are targeting with your project, as seen in the example above.

For other variants of SQL Server / Azure SQL Database there are dedicated packages as [listed here](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master#readme-body-tab).

## Circular References and SuppressMissingDependenciesErrors

In order to solve circular references between databases that may have been incorrectly set up, it is possible to add
`SuppressMissingDependenciesErrors` to both [Package References](#package-references) and [Project References](#project-references):

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeDatabase" SuppressMissingDependenciesErrors="True"/>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" DatabaseVariableLiteralValue="SomeOtherDatabase" SuppressMissingDependenciesErrors="True"/>
  </ItemGroup>
</Project>
```

## SQL CLR assembly references

The SDK can reference an existing SQL CLR assembly and embed it in the produced `.dacpac` via a `CREATE ASSEMBLY` statement, so you can author the `CREATE FUNCTION ... EXTERNAL NAME` (and similar) T-SQL objects that depend on it directly in your SQL project.

Build the SQL CLR code in a separate .NET Framework class library and add a `<ProjectReference>` (or a `<Reference>` to the resulting `.dll`) from your `MSBuild.Sdk.SqlProj` project:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <ItemGroup>
    <ProjectReference Include="..\MySqlClrLibrary\MySqlClrLibrary.csproj" />
  </ItemGroup>
</Project>
```

Any `.dll` flowing into the build via `<Reference>`, `<ProjectReference>`, or as a sibling of a referenced `.dacpac` is picked up automatically and added to the dacpac as a `CREATE ASSEMBLY` object. No permission set is specified, so SQL Server will use the default (`SAFE`).

### Requirements

- The referenced assembly must target **.NET Framework** (typically `net48`, or `netstandard2.0` consumed under .NET Framework). Modern .NET assemblies are not supported by SQL Server CLR.
- **.NET Framework 4.8 must be installed on the build machine.** When assemblies are referenced, the SDK shells out to a small Windows-only helper (`DacpacToolFramework.exe`, targeting `net48`) to create the Dacpac, because a Dacpac with an assembly can only be created on .NET Framework.

### Trusting CLR assemblies during deployment

When SQL Server has CLR strict security enabled (the default since SQL Server 2017), every assembly loaded via `CREATE ASSEMBLY` must either be signed or be registered in `sys.trusted_assemblies` before deployment. Set the `ClrAssemblyTrustInPreDeploy` MSBuild property to `True` to have the SDK automatically inject T-SQL into the dacpac's pre-deployment script that registers each referenced assembly via `sys.sp_add_trusted_assembly` (only when not already trusted):

```xml
<PropertyGroup>
  <ClrAssemblyTrustInPreDeploy>True</ClrAssemblyTrustInPreDeploy>
</PropertyGroup>
```

The `SHA2_512` hash of each referenced assembly is computed at build time and embedded as a 64-byte literal in the pre-deployment script, so the script remains small even for large assemblies (the assembly bytes themselves are only carried once, inside the corresponding `CREATE ASSEMBLY` statement). The principal performing the deployment must have `ALTER ANY DATABASE` (or sysadmin) permission to call `sp_add_trusted_assembly`; if it does not, the script throws an error containing the manual command an operator can run on the server to trust the assembly.

### Isolating SQL CLR objects in a separate project

If you do not want to mix CLR object definitions and regular schema in the same project, you can "isolate" your SQL CLR objects in a separate `.sqlproj`, build and pack the resulting `.dacpac` in a NuGet package on Windows, and then reference this package from your project. Read more about this approach in [this blog post](https://erikej.github.io/dacfx/sqlclr/2025/01/28/dacfx-sqlclr-msbuild-sdk-sqlproj.html).