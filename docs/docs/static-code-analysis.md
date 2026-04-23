# Static code analysis

Starting with version 2.7.0 of the SDK, there is support for running static code analysis during build. The SDK includes the following sets of rules:

- [Transact-SQL Design Issues](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd193411(v=vs.100))
- [Transact-SQL Naming Issues](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd193246(v=vs.100))
- [Transact-SQL Performance Issues](https://learn.microsoft.com/en-us/previous-versions/visualstudio/visual-studio-2010/dd172117(v=vs.100))

## Enable static code analysis

Static code analysis can be enabled by adding the `RunSqlCodeAnalysis` property to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
    <CodeAnalysisRules>-SqlServer.Rules.SRD0006;-Smells.*</CodeAnalysisRules>
  </PropertyGroup>
</Project>
```

> [!NOTE]
> The target framework must be set to `net8.0` or `net10.0` when using additional NuGet-based rules.

An XML file with the analysis results is created in the output folder.

The optional `CodeAnalysisRules` property allows you to disable individual rules or groups of rules for the entire project.

## Suppress rules per file

Starting with version 3.0.0 of the SDK, you can also disable rules per file. Add a `StaticCodeAnalysis.SuppressMessages.xml` file to the project root, with contents similar to this:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<StaticCodeAnalysis version="2" xmlns="urn:Microsoft.Data.Tools.Schema.StaticCodeAnalysis">
   <SuppressedFile FilePath="Procedures\sp_Test.sql">
     <SuppressedRule Category="Microsoft.Rules.Data" RuleId="SR0001" />
   </SuppressedFile>
</StaticCodeAnalysis>
```

Any rule violations found during analysis are reported as build warnings.

## Treat rule violations as errors

Individual rule violations or groups of rules can be configured to be reported as build errors as shown below.

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <RunSqlCodeAnalysis>True</RunSqlCodeAnalysis>
    <CodeAnalysisRules>+!SqlServer.Rules.SRN0005;+!SqlServer.Rules.SRD*</CodeAnalysisRules>
  </PropertyGroup>
</Project>
```

## Use additional rule packages

We know of the following public rules NuGet packages, that you can add to your project.

> These rule sets were included with the SDK in version 2.7.x and 2.8.x, but must be added explicitly with SDK version 2.9.x and later.

```xml
<ItemGroup>
  <PackageReference Include="ErikEJ.DacFX.SqlServer.Rules" Version="4.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="ErikEJ.DacFX.TSQLSmellSCA" Version="4.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

They are based on these older repositories:

- [SqlServer.Rules](https://github.com/tcartwright/SqlServer.Rules/blob/master/docs/table_of_contents.md)
- [Smells](https://github.com/davebally/TSQL-Smells)

## Create your own custom rule packages

You can also build your own rules. For an example of how to build a custom rule, see [this blog post](https://erikej.github.io/dacfx/dotnet/2024/04/04/dacfx-rules.html).

To publish your own custom rules, pack your rule .dll in a NuGet package as shown in this rule project file [from GitHub](https://github.com/ErikEJ/SqlServer.Rules/blob/master/src/SqlServer.Rules/SqlServer.Rules.csproj).
