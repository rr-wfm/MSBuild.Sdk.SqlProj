# MSBuild.Sdk.SqlProj

[![Build Status](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/actions/workflows/main.yml/badge.svg)](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/actions/workflows/main.yml)
[![Latest Release](https://img.shields.io/nuget/v/MSBuild.Sdk.SqlProj)](https://www.nuget.org/packages/MSBuild.Sdk.SqlProj)
[![Latest Prerelease](https://img.shields.io/nuget/vpre/MSBuild.Sdk.SqlProj)](https://www.nuget.org/packages/MSBuild.Sdk.SqlProj)
[![Downloads](https://img.shields.io/nuget/dt/MSBuild.Sdk.SqlProj)](https://www.nuget.org/packages/MSBuild.Sdk.SqlProj#statistics-body)

## Introduction

A MSBuild SDK that produces SQL Server Data-Tier Application packages (`.dacpac`) from SQL scripts using SDK-style .NET projects.

## Documentation

- [Documentation site](https://rr-wfm.github.io/MSBuild.Sdk.SqlProj/docs/getting-started.html)

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
