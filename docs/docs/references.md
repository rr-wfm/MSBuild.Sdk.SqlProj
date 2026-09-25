# References

## Package references

`MSBuild.Sdk.SqlProj` supports referencing NuGet packages that contain `.dacpac` packages. These can be referenced by using the `PackageReference` format familiar to .NET developers. They can also be installed through the NuGet Package Manager in Visual Studio.

### Basic package references

```xml
  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" />
  </ItemGroup>
```

It will assume that the `.dacpac` file is inside the `tools` folder of the referenced package and that it has the same name as the NuGet package. Referenced packages that do not adhere to this convention will be silently ignored. However, you have the ability to override this convention by using the `DacpacName` attribute on the `PackageReference`. For example:

### Override the dacpac name

```xml
  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DacpacName="SomeOtherDacpac" />
  </ItemGroup>
```

This will add a reference to the `tools\SomeOtherDacpac.dacpac` file inside the `MyDatabasePackage` package. Note that if that file doesn't exist within the package, the package reference will still be silently ignored. However, the build will most likely fail if your project actually references objects from the reference package.

By default, the package reference is treated as being part of the same database. For example, if the reference package contains a `.dacpac` that has a table and a stored procedure and you would `dotnet publish` the project the table and stored procedure from that package will be deployed along with the contents of your project to the same database. If this is not desired, you can add the `DatabaseVariableLiteralValue` item metadata to the `PackageReference` specifying a different database name:

### Reference another database

```xml
  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeOtherDatabase" />
  </ItemGroup>
```

In this scenario you can access the objects defined by `MyDatabasePackage` by using the `[SomeOtherDatabase].[<schema>].[<object>]` syntax.

You can also use SQLCMD variables to set references, similar to the behavior of a `.sqlproj` project, by adding `DatabaseSqlCmdVariable` and optionally `ServerSqlCmdVariable` item metadata to the `PackageReference` element:

> [!NOTE]
> Don't forget to define appropriate [SQLCMD variables](project-configuration.md#sqlcmd-variables)

### Use SQLCMD variables

```xml
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
  <ItemGroup>
    <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" />
  </ItemGroup>
```

This will ensure that `MyOtherProject` is built first and the resulting `.dacpac` will be referenced by this project. This means you can use the objects defined in the other project within the scope of this project. If the other project is representing an entirely different database, you can also use `DatabaseVariableLiteralValue` or SQLCMD variables on the `ProjectReference` similar to `PackageReference`:

### Reference another database

```xml
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
```

> [!NOTE]
> We do not support adding a `ProjectReference` to an existing `.sqlproj` file.

## Direct DACPAC file references

Reference an existing `.dacpac` file without a NuGet package or another project:

```xml
<ItemGroup>
  <ArtifactReference Include="../Database artifacts/Shared.dacpac">
    <HintPath>../Database artifacts/Shared.dacpac</HintPath>
  </ArtifactReference>
</ItemGroup>
```

`ArtifactReference` is the direct-file authoring item. Use the DACPAC path for both `Include` and `HintPath`, as shown above. When the `HintPath` element is omitted or its value is empty, the SDK uses the path in `Include`. Otherwise, it uses the path in `HintPath`. If the file at that path does not exist, the build fails; the SDK does not fall back to `Include`. The selected path may be absolute or relative to the consuming project directory, including spaces. With a nonempty `HintPath`, `Include` is the item identity and may differ from the file path.

The file must already exist when references are resolved; this item does not build or restore it. A missing file fails the build with a diagnostic naming the reference and selected path, even if compilation would otherwise be skipped. Use `ProjectReference` when the dependency should be built automatically.

By default, objects are referenced in the same database. The following metadata works just as it does for package and project references:

| Metadata | Purpose |
| --- | --- |
| `DatabaseVariableLiteralValue` | Reference a different database by its literal name. |
| `DatabaseSqlCmdVariable` | Reference a different database through a declared SQLCMD variable. |
| `ServerSqlCmdVariable` | Reference a different server through a declared SQLCMD variable; combine with a database literal or variable. |
| `SuppressMissingDependenciesErrors` | Suppress missing dependencies in the referenced model (`True`/`False`, defaults to `False`). This does not suppress missing-file errors. |

### Reference another database by name

For a fixed database name, set `DatabaseVariableLiteralValue`:

```xml
<ItemGroup>
  <ArtifactReference Include="references/Shared.dacpac">
    <HintPath>references/Shared.dacpac</HintPath>
    <DatabaseVariableLiteralValue>Shared</DatabaseVariableLiteralValue>
    <SuppressMissingDependenciesErrors>False</SuppressMissingDependenciesErrors>
  </ArtifactReference>
</ItemGroup>
```

SQL can then reference `[Shared].[dbo].[MyTable]`. To join objects from two referenced databases, add one `ArtifactReference` per DACPAC with its own database mapping.

You can also supply the referenced database name through an MSBuild property:

```xml
<ItemGroup>
  <ArtifactReference Include="references/Shared.dacpac">
    <DatabaseVariableLiteralValue>$(SharedDatabase)</DatabaseVariableLiteralValue>
  </ArtifactReference>
</ItemGroup>
```

```sh
dotnet build -t:Rebuild -p:SharedDatabase=Reporting
```

Here, `SharedDatabase` is a property defined by your project usage, not a built-in SDK setting. MSBuild substitutes `Reporting` into the reference metadata, allowing SQL to reference `[Reporting].[dbo].[MyTable]`. This sets the referenced database name used during compilation; it does not rename or deploy a database, or replace names in your SQL files. Your SQL must use the matching database name.

Use `-t:Rebuild` when changing reference settings through command-line properties because incremental compilation may not detect those changes. For database names that vary between deployment environments, use SQLCMD variables as shown below so you can supply the name at deployment without rebuilding the DACPAC.

### Use SQLCMD variables

```xml
<ItemGroup>
  <ArtifactReference Include="references/Shared.dacpac">
    <HintPath>references/Shared.dacpac</HintPath>
    <DatabaseSqlCmdVariable>SharedDatabase</DatabaseSqlCmdVariable>
    <ServerSqlCmdVariable>SharedServer</ServerSqlCmdVariable>
    <SuppressMissingDependenciesErrors>False</SuppressMissingDependenciesErrors>
  </ArtifactReference>
  <SqlCmdVariable Include="SharedDatabase" DefaultValue="Shared" />
  <SqlCmdVariable Include="SharedServer" DefaultValue="localhost" />
</ItemGroup>
```

SQL can then reference `[$(SharedServer)].[$(SharedDatabase)].[dbo].[MyTable]`. Declare the SQLCMD variables separately; reference metadata does not create them. For a fixed database name, use `DatabaseVariableLiteralValue="Shared"` instead of `DatabaseSqlCmdVariable`.

Referenced files are copied to the build output alongside the consuming DACPAC. They are also copied to the output of projects that reference the consuming project. To prevent a referenced file from being copied, set `Private` to `false`:

```xml
<ArtifactReference Include="references/Shared.dacpac">
  <Private>false</Private>
</ArtifactReference>
```

Omitting `Private` or setting it to `true` enables copying. Changing `Private` to `false` does not remove copies left by previous builds.

A newer referenced file triggers recompilation, including when `Private` is `false`. All copied references use the same output directory, so use distinct filenames to avoid collisions.

Declare each dependency once. For example, do not add an `ArtifactReference` for a DACPAC already referenced through a package or project; duplicate references are not automatically removed.

The syntax follows Microsoft's documented `ArtifactReference`/`HintPath` shape, with the path selection rules described above. Microsoft recommends project/package references for new development. Add direct references by editing the project file; Visual Studio reference-picker integration and automatic lookup of installed system DACPACs are not supported.

## Referencing system databases

Microsoft has released NuGet packages containing the definitions of the `master` and `msdb` databases. This is useful if you want to reference objects from those databases within your own projects without getting warnings. To reference these, you'll need to use the `DacpacName` feature for package references described above. For example:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.SqlServer.Dacpacs.Master" Version="160.2.7" DacpacName="master" DatabaseVariableLiteralValue="master" />
  </ItemGroup>
```

The above example references the `master` database from the [Microsoft.SqlServer.Dacpacs.Master](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master) NuGet package. Please note that there are different major versions of that package for different versions of SQL Server. It is recommended to reference the most recent minor/patch version of the package as the `SqlServerVersion` you are targeting with your project, as seen in the example above.

For other variants of SQL Server / Azure SQL Database there are dedicated packages as [listed here](https://www.nuget.org/packages/Microsoft.SqlServer.Dacpacs.Master#readme-body-tab).

## Circular References and SuppressMissingDependenciesErrors

In order to solve circular references between databases that may have been incorrectly set up, it is possible to add
`SuppressMissingDependenciesErrors` to both [Package References](#package-references) and [Project References](#project-references):

```xml
  <ItemGroup>
    <PackageReference Include="MyDatabasePackage" Version="1.1.0" DatabaseVariableLiteralValue="SomeDatabase" SuppressMissingDependenciesErrors="True"/>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../MyOtherProject/MyOtherProject.csproj" DatabaseVariableLiteralValue="SomeOtherDatabase" SuppressMissingDependenciesErrors="True"/>
  </ItemGroup>
```
