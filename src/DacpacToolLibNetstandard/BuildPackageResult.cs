namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

using Microsoft.SqlServer.Dac.Model;

public class BuildPackageResult
{
    public BuildPackageResult(TSqlModel model, bool hasValidationErrors)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        HasValidationErrors = hasValidationErrors;
    }

    public TSqlModel Model { get; private set; }

    public bool HasValidationErrors { get; private set; }
}