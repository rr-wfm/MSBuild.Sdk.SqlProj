# Packaging

`MSBuild.Sdk.SqlProj` version 2.8.1 and later supports packaging your project into a [NuGet](https://www.nuget.org) package using the `dotnet pack` command.

## Package your project

You'll need to set the `PackageProjectUrl` property in the `.csproj` like this:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
    <PackageProjectUrl>your-project-url</PackageProjectUrl>
  </PropertyGroup>
</Project>
```

Other metadata for the package can be controlled by using the [documented](https://docs.microsoft.com/dotnet/core/tools/csproj#nuget-metadata-properties) properties in your project file.

## Packaging standalone dacpacs

If you have an already-compiled `.dacpac` file without a corresponding `.csproj` that you need to reference as a `PackageReference`, you can use existing NuGet functionality to wrap the dacpac in a NuGet package. To do that, create a `.nuspec` file referencing your dacpac:

### Create a `.nuspec` file

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

### Build the package

```bash
nuget pack fileName.nuspec
```

Then push the package to your local NuGet repository:

### Publish the package

```bash
nuget push \
  fileName.version.nupkg \
  -Source /your/nuget/repo/path
```

You can now reference your dacpac as a `PackageReference`!

> [!NOTE]
> To run these commands, you'll need to have the NuGet CLI tools installed. See [these installation instructions](https://docs.microsoft.com/nuget/install-nuget-client-tools#nugetexe-cli). If you use Chocolatey, you can also install by running `choco install nuget.commandline`. On a Mac with Homebrew installed, use `brew install nuget`.
