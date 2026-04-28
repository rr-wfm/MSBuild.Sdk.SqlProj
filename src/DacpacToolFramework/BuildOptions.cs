
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;
using DotMake.CommandLine;
using System.Threading.Tasks;

namespace MSBuild.Sdk.SqlProj.DacpacToolFramework
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used a DTO")]
    [CliCommand(Description = "Build a SQL Server Data-Tier Application Framework package (dacpac)", 
                Name = "build")]
    public class BuildOptions : BuildOptionsBase
    {
        public async Task<int> RunAsync()
        {
            return await Program.BuildDacpac(this).ConfigureAwait(false);
        }    
    }    
}

