using Aspire.Hosting.ApplicationModel;

namespace MSBuild.Sdk.SqlProj.Aspire;

public record DacpacMetadataAnnotation(string DacpacPath) : IResourceAnnotation
{
}
