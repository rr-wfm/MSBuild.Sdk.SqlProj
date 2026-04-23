namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

using Microsoft.SqlServer.Dac.Model;

public sealed class BuildPackageResult : IDisposable
{
    public BuildPackageResult(TSqlModel model, bool hasValidationErrors)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        HasValidationErrors = hasValidationErrors;
    }

    public TSqlModel Model { get; private set; }

    public bool HasValidationErrors { get; private set; }

    public void Dispose()
    {
        Model?.Dispose();
        Model = null;
    }
}