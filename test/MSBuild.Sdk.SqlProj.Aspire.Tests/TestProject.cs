using Aspire.Hosting;

namespace MSBuild.Sdk.SqlProj.Aspire.Tests;

internal class TestProject : IProjectMetadata
{
    public string ProjectPath { get; } = "../../../../TestProject/TestProject.csproj";
}
