using Aspire.Hosting.ApplicationModel;

namespace MSBuild.Sdk.SqlProj.Aspire;

public record WaitForSqlProjectAnnotation(SqlProjectResource Resource) : IResourceAnnotation
{ 
}
