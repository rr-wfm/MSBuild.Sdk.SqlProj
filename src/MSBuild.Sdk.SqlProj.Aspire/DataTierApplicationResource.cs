using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Build.Evaluation;

namespace MSBuild.Sdk.SqlProj.Aspire;

public sealed class DataTierApplicationResource(string name) : Resource(name)
{
    public string GetDacpacPath()
    {
        var projectMetadata = Annotations.OfType<IProjectMetadata>().Single();
        var projectPath = projectMetadata.ProjectPath;
        var project = new Project(projectPath);
        return project.GetPropertyValue("TargetPath");
    }
}
