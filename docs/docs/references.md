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
