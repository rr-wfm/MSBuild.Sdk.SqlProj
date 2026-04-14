# Introduction

A MSBuild SDK that is capable of producing a SQL Server Data-Tier Application package (.dacpac) from a set of SQL scripts that can be subsequently deployed using the `Microsoft.SqlPackage` [dotnet tool](https://www.nuget.org/packages/Microsoft.SqlPackage). It provides much of the same functionality as the SQL Server Data Tools .sqlproj project format, but is built on top of the new SDK-style projects that were first introduced in Visual Studio 2017.

If you're looking for a video introduction, please watch this [dotnetFlix episode](https://dotnetflix.com/player/104). For some more background on this project read the following blogposts:

- [Introducing MSBuild.Sdk.SqlProj](https://jmezach.github.io/post/introducing-msbuild-sdk-sqlproj/)
- [An update on MSBuild.Sdk.SqlProj](https://jmezach.github.io/post/update-on-msbuild-sdk-sqlproj/)
