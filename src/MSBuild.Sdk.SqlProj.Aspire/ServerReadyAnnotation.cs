using Aspire.Hosting.ApplicationModel;

namespace MSBuild.Sdk.SqlProj.Aspire
{
    public record ServerReadyAnnotation() : IResourceAnnotation
    {
        public int Attempt { get; set; } = 1;
        public bool Published { get; set; }
        public bool Connected { get; set; }
    }
}
