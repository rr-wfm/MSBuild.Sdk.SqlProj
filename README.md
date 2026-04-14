# MSBuild.Sdk.SqlProj

![Build Status](https://github.com/jmezach/MSBuild.Sdk.SqlProj/workflows/CI/badge.svg)
![Latest Stable Release](https://img.shields.io/nuget/v/MSBuild.Sdk.SqlProj)
![Latest Prerelease](https://img.shields.io/nuget/vpre/MSBuild.Sdk.SqlProj)
![Downloads](https://img.shields.io/nuget/dt/MSBuild.Sdk.SqlProj)

## Introduction 

A MSBuild SDK that produces SQL Server Data-Tier Application packages (`.dacpac`) from SQL scripts using SDK-style .NET projects.

## Documentation

- Documentation site: https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/

## Code of conduct

- Code of conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

## Quick Start

Install the project templates:

```bash
dotnet new install MSBuild.Sdk.SqlProj.Templates
```

Create a new SQL project:

```bash
dotnet new sqlproj
```

Build the project:

```bash
dotnet build
```

For installation details, project configuration, references, packaging, publishing, and advanced topics, use the documentation site links above.
