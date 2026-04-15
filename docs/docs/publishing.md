# Publishing

Starting with MSBuild.Sdk.SqlProj version 4.0.0 there are two modes of publishing supported: publishing the database directly from the project to a SQL Server instance, or publishing a container image that includes both SqlPackage and the .dacpac ready to be run anywhere a container can be executed.

> [!NOTE]
> For 4.0.0, to support both modes we unfortunately had to make a breaking change to the SDK. If you've previously used `dotnet publish` to deploy your database directly to SQL Server you'll now need to add `/t:PublishDatabase` to your command line to retain the previous behavior.

We generally recommend using the container image approach for most scenarios, as it provides a consistent deployment experience across different environments. However, for local development and quick deployments the direct publishing approach might be more convenient.

## Publishing directly to SQL Server

There is support for publishing a project to a SQL Server using the `dotnet publish /t:PublishDatabase` command. This support is designed to be used by developers to deploy or update their local development database quickly. For more advanced deployment scenarios we suggest using [SqlPackage](https://docs.microsoft.com/en-us/sql/tools/sqlpackage/sqlpackage?view=sql-server-ver15) instead as it provides more options, or check out the container image approach described below.

There are a couple of properties that control the deployment process which have some defaults to make the experience as smooth as possible for local development. For example, on Windows if you have a default SQL Server instance running on your local machine running `dotnet publish /t:PublishDatabase` creates a database with the same name as the project. Unfortunately on Mac and Linux we cannot use Windows authentication, so you'll need to specify a username and password:

```bash
dotnet publish \
  /t:PublishDatabase \
  /p:TargetUser=<username> \
  /p:TargetPassword=<password>
```

To further customize the deployment process, you can use the following properties which can either be set in the project file or specified on the command line (using the `/p:<property>=<value>` syntax shown above).

| Property | Default Value | Description |
| --- | --- | --- |
| TargetServerName | (local) | Controls the name of the server to which the project is published. Defaults to '(localdb)\mssqllocaldb' when using Visual Studio. |
| TargetDatabaseName | Project name | Controls the name of the database published by `dotnet publish` |
| TargetPort |  | Specifies an alternate port for connecting to the target server (only necessary if using a non-standard port) |
| TargetUser |  | Username used to connect to the server. If empty, Windows authentication is used |
| TargetPassword | | Password used to connect to the server. If empty, but TargetUser is set you will be prompted for the password |
| IncludeCompositeObjects | True | Controls whether objects from referenced packages are deployed to the same database |
| TargetName | Project name | Controls the name of the `.dacpac` created by `dotnet build`. The default name for the `.dacpac` file is the name of the project file, e.g. `MyProject.csproj` produces `MyProject.dacpac`. |
| DeployOnPublish | True | Controls whether a deploy occurs when the project is published. |

> [!IMPORTANT]
> Although you can set the username and password in your project file we don't recommend doing so since you'll be committing credentials to version control. Instead, you should specify these at the command line when needed.

In addition to these properties, you can also set any of the [documented](https://docs.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacdeployoptions) deployment options. These are typically set in the project file, for example:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
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
| SqlCommandVariableValues | | These should not be set as a Property, but instead as an ItemGroup as described [in this section](project-configuration.md#sqlcmd-variables) |

## Publishing as a container image

From version 4.0.0 of MSBuild.Sdk.SqlProj we now support publishing your database project as a runnable container image. The image will contain both SqlPackage and the .dacpac file. This allows you to run the image anywhere a container can be executed, making it ideal for CI/CD pipelines and other automated deployment scenarios.

> [!NOTE]
> By default the published container will contain the latest version of SqlPackage available at the time of publishing. If you want to pin the specific version of SqlPackage used in the container you can set the `SqlPackageDownloadUrl` property in your project file to point to the specific version you want, ie: `https://go.microsoft.com/fwlink/?linkid=2338525` for version 170.2.70. You can find the appropriate download links on the [SqlPackage release notes](https://learn.microsoft.com/en-us/sql/tools/sqlpackage/release-notes-sqlpackage) page. Please make sure to use the link for the Linux .NET 8 version of SqlPackage.

To publish your project as a container image, use the following command:

```bash
dotnet publish /t:PublishContainer
```

By default, the image will be tagged with the name of your project. You can customize the image name and tag by setting the `ContainerRepository` and `ContainerImageTag` properties on the command line. For example:

```bash
dotnet publish \
  /t:PublishContainer \
  /p:ContainerRepository=my-database-image \
  /p:ContainerImageTag=v1.0.0
```

You can also set these properties in your project file if you prefer:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    ...
    <ContainerRepository>my-database-image</ContainerRepository>
    <ContainerImageTag>v1.0.0</ContainerImageTag>
    ...
  </PropertyGroup>
</Project>
```

Once the container image is published, you can run it using the following command:

```bash
docker run \
  my-database-image:v1.0.0 \
  --rm \
  /TargetConnectionString=<your-connection-string>
```

This will execute SqlPackage inside the container, deploying the .dacpac to the specified SQL Server instance. You can pass any additional SqlPackage parameters as needed. However by default the container is configured to pass `/Action:Publish` and `/SourceFile:<your-dacpac-file>.dacpac`. For a full list of available parameters, refer to the [SqlPackage documentation](https://learn.microsoft.com/en-us/sql/tools/sqlpackage/sqlpackage?view=sql-server-ver17).

> [!IMPORTANT]
> Since SqlPackage currently only supports an x64 architecture the container image being published is also x64. Additionally we only support Linux-based containers at this time.

## Script generation

Instead of using `dotnet publish /t:PublishDatabase` to deploy changes to a database, you can also have a full SQL script generated that will create the database from scratch and then run that script against a SQL Server. This can be achieved by adding the following to the project file:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/4.2.0">
  <PropertyGroup>
    <GenerateCreateScript>True</GenerateCreateScript>
    <IncludeCompositeObjects>True</IncludeCompositeObjects>
  </PropertyGroup>
</Project>
```

With this enabled you'll find a SQL script with the name `<database-name>_Create.sql` in the bin folder. When the project is referenced by another project, the generated script is also copied to the referencing project's output directory alongside the `.dacpac`.
The database name for the create script gets resolved in the following manner:

1. `TargetDatabaseName`.
1. Package name.

> [!NOTE]
>
> - the generated script also uses the resolved database name via a setvar command.
> - if `IncludeCompositeObjects` is true, the composite objects (tables, etc.) from external references are also included in the generated script. This property defaults to `true`
