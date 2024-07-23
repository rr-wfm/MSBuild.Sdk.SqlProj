using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Build.Evaluation;

namespace MSBuild.Sdk.SqlProj.Aspire;

public sealed class DataTierApplicationResource(string name) : Resource(name)
{
    public string GetDacpacPath()
    {
        var projectMetadata = Annotations.OfType<IProjectMetadata>().FirstOrDefault();
        if (projectMetadata != null)
        {
            var projectPath = projectMetadata.ProjectPath;
            var project = new Project(projectPath);
            return project.GetPropertyValue("TargetPath");
        }

        var dacpacMetadata = Annotations.OfType<DacpacMetadataAnnotation>().FirstOrDefault();
        if (dacpacMetadata != null)
        {
            return dacpacMetadata.DacpacPath;
        }

        throw new InvalidOperationException($"Unable to locate data-tier application package for resource {Name}.");
    }
}
