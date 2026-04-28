namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

using System.Text.RegularExpressions;
using Microsoft.SqlServer.Dac.Model;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections;
using System.Text;

public static class PackageBuildExtensions
{
    static Type CustomSchemaDataType;

    private static readonly Regex ExternalPartsRegex = new Regex(@"dbl=(?<dbl>\w+)|dbv=(?<dbv>\w+)|srv=(?<srv>\w+)", 
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    static MethodInfo SetMetadataMethod;
    
#pragma warning disable CA1810 // Initialize reference type static fields inline
    static PackageBuildExtensions()
#pragma warning restore CA1810 // Initialize reference type static fields inline
    {
        var customType = Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql");
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
        if (customType == null)
        {
            throw new InvalidOperationException("Unable to load Microsoft.Data.Tools.Schema.Sql assembly.");
        }

        CustomSchemaDataType = customType;

        var customData = Activator.CreateInstance(customType, "Reference", "SqlSchema");

        if (customData == null)
        {
            throw new InvalidOperationException("Unable to create instance of CustomSchemaData.");
        }

        var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);

        if (setMetadataMethod == null)
        {
            throw new InvalidOperationException("Unable to find SetMetadata method on CustomSchemaData.");
        }
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
        SetMetadataMethod = setMetadataMethod;
    }

    public static void AddReference(this TSqlModel model, string referencePath, string externalParts, bool suppressErrorsForMissingDependencies)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(referencePath);

        var dataSchemaModel = GetDataSchemaModel(model);

        var crossPlatformPath = referencePath.Replace('\\', Path.AltDirectorySeparatorChar);

        var customData = Activator.CreateInstance(CustomSchemaDataType, "Reference", "SqlSchema");

        if (customData == null)
        {
            throw new InvalidOperationException("Unable to create instance of CustomSchemaData.");
        }

        SetMetadataMethod.Invoke(customData, new object[] { "FileName", crossPlatformPath });
        SetMetadataMethod.Invoke(customData, new object[] { "LogicalName", Path.GetFileName(crossPlatformPath) });
        SetMetadataMethod.Invoke(customData,
            new object[] { "SuppressMissingDependenciesErrors", suppressErrorsForMissingDependencies.ToString() });

        if (!string.IsNullOrWhiteSpace(externalParts))
        {
            var parts = ParseExternalParts(externalParts);
            if (!string.IsNullOrEmpty(parts))
            {
                SetMetadataMethod.Invoke(customData, new object[] {"ExternalParts", parts});
            }
        }

        AddCustomData(dataSchemaModel, customData);
    }    

    private static object GetDataSchemaModel(TSqlModel model)
    {
        var serviceField = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance);

        if (serviceField == null)
        {
            throw new InvalidOperationException("Unable to find _service field on TSqlModel.");
        }

        var service = serviceField.GetValue(model);

        if (service == null)
        {
            throw new InvalidOperationException("Unable to get _service field value from TSqlModel.");
        }

        var dataSchemaModelProperty  = service.GetType().GetProperty("DataSchemaModel", BindingFlags.NonPublic | BindingFlags.Instance);

        if (dataSchemaModelProperty == null)
        {
            throw new InvalidOperationException("Unable to find DataSchemaModel property on service.");
        }

        var dataSchemaModel = dataSchemaModelProperty.GetValue(service);

        if (dataSchemaModel == null)
        {
            throw new InvalidOperationException("Unable to get DataSchemaModel property value from service.");
        }

        return dataSchemaModel;
    }

    private static string ParseExternalParts(string externalParts)
    {
        return ParseExternalParts(externalParts, ExternalPartsRegex);
    }

    private static string ParseExternalParts(string externalParts, Regex externalPartsRegex)
    {
        string serverVariableName = null;
        string databaseVariableName = null;
        string databaseVariableLiteralValue = null;

        // If there are '=' sign in argument assumes that this is formula, else assume that a single value passed and that it is database literal.
        if (externalParts.Contains('='))
        {
            try
            {
                foreach (Match match in externalPartsRegex.Matches(externalParts))
                {
                    if (match.Groups["dbl"].Success)
                    {
                        databaseVariableLiteralValue = Identifier.EncodeIdentifier(match.Groups["dbl"].Value);
                    }
                    else if (match.Groups["dbv"].Success)
                    {
                        databaseVariableName =
                            Identifier.EncodeIdentifier(EnsureIsDelimited(match.Groups["dbv"].Value));
                    }
                    else if (match.Groups["srv"].Success)
                    {
                        serverVariableName = Identifier.EncodeIdentifier(EnsureIsDelimited(match.Groups["srv"].Value));
                    }
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new ArgumentException(
                    "Unable to parse reference external parts. " +
                    "Use a database literal or SQLCMD variable metadata such as " +
                    "'DatabaseVariableLiteralValue=\"MyDatabase\"', " +
                    "'DatabaseSqlCmdVariable=\"MyDatabaseVar\"', or " +
                    "'DatabaseSqlCmdVariable=\"MyDatabaseVar\" ServerSqlCmdVariable=\"MyServerVar\"'.",
                    nameof(externalParts),
                    ex);
            }
        }
        else
        {
            databaseVariableLiteralValue = Identifier.EncodeIdentifier(externalParts);
        }

        // if there are no value for database return. Reference MUST have database value
        if (string.IsNullOrEmpty(databaseVariableLiteralValue) && string.IsNullOrEmpty(databaseVariableName))
            return null;

        //if either literal and variable specified for database (that is wrong situation), literal has the priority
        return (string.IsNullOrEmpty(serverVariableName) ? "" : serverVariableName + ".") + 
                    (string.IsNullOrEmpty(databaseVariableLiteralValue) ? databaseVariableName : databaseVariableLiteralValue);
    }

    public static string Format(this DacModelException exception, string fileName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var stringBuilder = new StringBuilder();

        foreach (var modelError in exception.Messages)
        {
            stringBuilder.Append(fileName);
            stringBuilder.Append('(');
            stringBuilder.Append('1');
            stringBuilder.Append(',');
            stringBuilder.Append('1');
            stringBuilder.Append("):");
            stringBuilder.Append(' ');
            stringBuilder.Append("Error");
            stringBuilder.Append(' ');
            stringBuilder.Append(modelError.Prefix);
            stringBuilder.Append(modelError.Number);
            stringBuilder.Append(": ");
            stringBuilder.AppendLine(modelError.Message);
        }

        return stringBuilder.ToString().Trim();
    }

    /// <summary>
    /// Cached method info for FileUtils.EnsureIsDelimited
    /// </summary>
    private static MethodInfo _ensureIsDelimitedMethod;

    /// <summary>
    /// This method found in Microsoft.Data.Tools.Utilities in class FileUtils. because of it is internal we do call through Reflection
    /// </summary>
    private static string EnsureIsDelimited(string name)
    {
        if (_ensureIsDelimitedMethod == null)
        {
            var fileUtilsType = Type.GetType("Microsoft.Data.Tools.Schema.Common.FileUtils, Microsoft.Data.Tools.Utilities");

            if (fileUtilsType == null)
            {
                throw new InvalidOperationException("Unable to load Microsoft.Data.Tools.Utilities assembly.");
            }

            _ensureIsDelimitedMethod = fileUtilsType
                .GetMethod("EnsureIsDelimited", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, new[]{ typeof(string) }, null);
        }

        if (_ensureIsDelimitedMethod == null)
        {
            throw new InvalidOperationException("Unable to find EnsureIsDelimited method in Microsoft.Data.Tools.Utilities assembly.");
        }

        return (string)_ensureIsDelimitedMethod.Invoke(null, new object[]{ name });
    }

    private static void AddCustomData(object dataSchemaModel, object customData)
    {
        var addCustomDataMethod = dataSchemaModel.GetType().GetMethod("AddCustomData", BindingFlags.Public | BindingFlags.Instance);

        if (addCustomDataMethod == null)
        {
            throw new InvalidOperationException("Unable to find AddCustomData method on DataSchemaModel.");
        }

        addCustomDataMethod.Invoke(dataSchemaModel, new[] { customData });
    }

    public static IEnumerable<string> GetReferencedDacPackages(this TSqlModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var result = new List<string>();
        var dataSchemaModel = GetDataSchemaModel(model);

        var getCustomDataMethod = dataSchemaModel.GetType().GetMethod("GetCustomData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(string) }, null);

        if (getCustomDataMethod == null)
        {
            throw new InvalidOperationException("Unable to find GetCustomData method on DataSchemaModel.");
        }

        var references = (IEnumerable) getCustomDataMethod.Invoke(dataSchemaModel, new object[] { "Reference", "SqlSchema" });

        if (references == null)
        {
            return result;
        }

        MethodInfo getMetadataMethod = null;
        foreach (var reference in references)
        {
            if (getMetadataMethod == null)
            {
                getMetadataMethod = reference.GetType().GetMethod("GetMetadata", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            }

            if (getMetadataMethod == null)
            {
                throw new InvalidOperationException("Unable to find GetMetadata method on reference.");
            }

            var fileName = (string)getMetadataMethod.Invoke(reference, new object[] { "FileName" });
            if (!string.IsNullOrEmpty(fileName))
            {
                result.Add(fileName);
            }
        }

        return result;
    }

    public static void AddSqlCmdVariables(this TSqlModel model, string[] variables)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(variables);

        var dataSchemaModel = GetDataSchemaModel(model);

        var customData = Activator.CreateInstance(CustomSchemaDataType, "SqlCmdVariables", "SqlCmdVariable");

        if (customData == null)
        {
            throw new InvalidOperationException("Unable to create instance of CustomSchemaData.");
        }

        foreach (var variableName in variables)
        {
            Console.WriteLine($"Adding SqlCmd variable {variableName}");
            SetMetadataMethod.Invoke(customData, new object[] { variableName, string.Empty });
        }

        AddCustomData(dataSchemaModel, customData);
    }

    public static IEnumerable<ModelValidationError> GetModelValidationErrors(this TSqlModel model, IEnumerable<string> ignoreValidationErrrors)
    {
        ArgumentNullException.ThrowIfNull(model);

        var serviceField = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance);

        if (serviceField == null)
        {
            throw new InvalidOperationException("Unable to find _service field on TSqlModel.");
        }

        var service = serviceField.GetValue(model);

        if (service == null)
        {
            throw new InvalidOperationException("Unable to get _service field value from TSqlModel.");
        }

        var getModelValidationErrorsMethod = service.GetType().GetMethod("GetModelValidationErrors", BindingFlags.NonPublic | BindingFlags.Instance);

        if (getModelValidationErrorsMethod == null)
        {
            throw new InvalidOperationException("Unable to find GetModelValidationErrors method on service.");
        }

        var modelValidationErrors = getModelValidationErrorsMethod.Invoke(service, new object[] { ignoreValidationErrrors }) as IEnumerable<object>;

        if (modelValidationErrors == null)
        {
            throw new InvalidOperationException("Unable to get model validation errors from service.");
        }

        var createDacModelErrorMethod = service.GetType().GetMethod("CreateDacModelError", BindingFlags.NonPublic | BindingFlags.Instance);

        if (createDacModelErrorMethod == null)
        {
            throw new InvalidOperationException("Unable to find CreateDacModelError method on service.");
        }

        var result = new List<ModelValidationError>();
        PropertyInfo documentProperty = null;
        foreach (var modelValidationError in modelValidationErrors)
        {
            if (documentProperty == null)
            {
                documentProperty = modelValidationError.GetType().GetProperty("Document", BindingFlags.Public | BindingFlags.Instance);
            }

            if (documentProperty == null)
            {
                throw new InvalidOperationException("Unable to find Document property on model validation error.");
            }

            var dacModelError = createDacModelErrorMethod.Invoke(service, new[] { modelValidationError }) as DacModelError;
            result.Add(new ModelValidationError(dacModelError, documentProperty.GetValue(modelValidationError) as string));
        }

        return result;
    }

    
}