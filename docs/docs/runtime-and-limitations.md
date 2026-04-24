# Runtime and Limitations

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

## Reference `MSBuild.Sdk.SqlProj` from class library

The output of `MSBuild.Sdk.SqlProj` is not an assembly, but a `.dacpac`. In order to correctly reference a `MSBuild.Sdk.SqlProj` based project from a class library, the `ReferenceOutputAssembly` hint needs to be set to `False`:

```xml
<ItemGroup>
  <ProjectReference
    Include="../MyDacpacProj/MyDacpacProj.csproj"
    ReferenceOutputAssembly="False" />
</ItemGroup>
```

Now, upon compilation of the class library, the relevant `.dacpac` files get copied to the output directory.

## Refactor Log support

While the SDK does not help you maintain a [refactor log](https://learn.microsoft.com/sql/ssdt/how-to-use-rename-and-refactoring-to-make-changes-to-your-database-objects), you can use an existing one during build by referring to it in your project:

```xml
<ItemGroup>
  <RefactorLog Include="RefactorLog\TestProjectWithPrePost.refactorlog" />
</ItemGroup>
```

## SQL CLR objects support

The SDK can reference an existing SQL CLR assembly and embed it in the produced `.dacpac` via `CREATE ASSEMBLY`. See [SQL CLR assembly references](references.md#sql-clr-assembly-references) for usage.

Note that adding assembly references requires **.NET Framework 4.8 on the build machine**, because the a Dacpac with an assembly can only be created on .NET Framework.

## Known limitations

Since this is not an entire project system but only an MSBuild SDK we cannot provide IntelliSense for objects defined within the project. This limitation can be circumvented by connecting the SQL editor to a live database that is used for development purposes.
