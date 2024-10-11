using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Build.Evaluation;

namespace MSBuild.Sdk.SqlProj.Aspire;

public sealed class SqlProjectResource(string name) : Resource(name)
{
    public string GetDacpacPath()
    {
        var projectMetadata = Annotations.OfType<IProjectMetadata>().FirstOrDefault();
        if (projectMetadata != null)
        {
            var projectPath = projectMetadata.ProjectPath;
            using var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);

            // .sqlprojx has a SqlTargetPath property, so try that first
            var targetPath = project.GetPropertyValue("SqlTargetPath");
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = project.GetPropertyValue("TargetPath");
            }

            return targetPath;
        }

        var dacpacMetadata = Annotations.OfType<DacpacMetadataAnnotation>().FirstOrDefault();
        if (dacpacMetadata != null)
        {
            return dacpacMetadata.DacpacPath;
        }

        throw new InvalidOperationException($"Unable to locate SQL Server Database project package for resource {Name}.");
    }
}
