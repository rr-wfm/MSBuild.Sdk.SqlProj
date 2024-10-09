using Aspire.Hosting.ApplicationModel;

namespace MSBuild.Sdk.SqlProj.Aspire;

public record TargetDatabaseResourceAnnotation(string TargetDatabaseResourceName) : IResourceAnnotation
{
}
