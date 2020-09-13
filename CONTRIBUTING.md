# Introduction
First off, thank you for considering contributing to MSBuild.Sdk.SqlProj. It's people like you that make this such a great project. Following these guidelines helps to communicate that you respect the time of the developers managing and developing this open source project. In return, they should reciprocate that respect in addressing your issue, assessing changes, and helping you finalize your pull requests.

You can contribute anything to this project, from improving documentation and triaging bugs to modifying the code. All of these are valuable contributions and you shouldn't hesitate to do any of those things.

# Ground Rules
Make sure you read through our [code of conduct](CODE_OF_CONDUCT.md) first and make sure you understand it before contributing.

In addition, consider the following when contributing:
* Ensure cross-platform compatibility for every change that's accepted. Windows, Mac, Linux.
* Create issues for any major changes and enhancements that you wish to make before sending a PR. Discuss things transparently and get community feedback.
* Keep feature versions as small as possible, preferably one new feature per version.
* Make sure you update the README.md for any new features you introduce.
* New features should be added with ample tests to ensure it works correctly.

# Your First Contribution
If this is the first time your considering contributing to this project please have a look at the [help wanted issues](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22). Don't hesitate to comment on those issues if you need help or guidance on how to get started with those issues. We're here to help you.

If you have never contributed to an open source project at all, have a look at http://makeapullrequest.com/, http://www.firsttimersonly.com/ and [How to Contribute to an Open Source Project on GitHub](https://egghead.io/series/how-to-contribute-to-an-open-source-project-on-github) for some help and guidance.

At this point, you're ready to make your changes! Feel free to ask for help; everyone is a beginner at first :smile_cat:

If a maintainer asks you to "rebase" your PR, they're saying that a lot of code has changed, and that you need to update your branch so it's easier to merge.

# Getting started
In order to get started contributing code, make sure you have the following installed on your machine:

* .NET Core 3.1 SDK
* Optionally: A local [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-2019) (possibly as a [Docker container](https://hub.docker.com/_/microsoft-mssql-server))

This project is made up of a [command line tool](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/tree/master/src/DacpacTool) that does most of the heavy lifting and an [accompanying NuGet package](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/tree/master/src/MSBuild.Sdk.SqlProj) that puts it all together. When you make changes to the `DacpacTool` you'll want to run a `dotnet build` in the `src/MSBuild.Sdk.SqlProj` folder. This ensures you're using the latest bits when building any of the [test projects](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/tree/master/test). There is a set of [unit tests](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/tree/master/test/DacpacTool.Tests) for the command line tool as well. For new features it is recommended to add new test projects in the `test` folder to ensure that the functionality works as intended.

If you want to debug the `DacpacTool` locally you can run `dotnet build /p:MSBuildSdkSqlProjDebug=True` on a project that uses the SDK. This will trigger the `DacpacTool` to wait for a debugger to attach before doing any actual work. Note that this only works when the `DacpacTool` is compiled for the `Debug` configuration.

When you're ready to make code changes, follow these steps:
1. Create your own fork of the code
2. Do the changes in your fork
3. If you like the change and think the project could use it:
    * Be sure you have followed the code style for the project.
    * Note the [Code of Conduct](CODE_OF_CONDUCT.MD).
    * Send a pull request.
    * Make sure that the CI pipeline is green.

# How to report a bug
If you find a security vulnerability, do **NOT** open an issue. Email opensource@rr-wfm.com instead.

For all other bugs, please report them through the [GitHub issue tracker](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues). Make sure you include at least the version of MSBuild.Sdk.SqlProj you're using as well as the platform you encountered the bug on (ie. Windows, Mac, Linux). Describe what you did, what you expected to see and what you see instead.

# How to suggest a feature or enhancement
Feel free to provide feature suggestions or enhancements to this project through the [GitHub issue tracker](https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/issues) as well. Note that this project currently isn't our top priority, so it could very well take a long time before we get time to implement it. If it is important to you, please consider contributing a PR for the feature.

# Code review process
Before any change is merged it will be reviewed by someone from the team.