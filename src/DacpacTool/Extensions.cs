using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
#pragma warning disable CA1724 // Type names should not match namespaces
    public static class Extensions
#pragma warning restore CA1724 // Type names should not match namespaces
    {
        static Type CustomSchemaDataType;

        static MethodInfo SetMetadataMethod;

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static Extensions()
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

        public static string Format(this BatchErrorEventArgs args, string source)
        {
            ArgumentNullException.ThrowIfNull(args);

            var outputMessageBuilder = new StringBuilder();
            outputMessageBuilder.Append(source);
            outputMessageBuilder.Append('(');
            outputMessageBuilder.Append(args.Line);
            outputMessageBuilder.Append(',');
            outputMessageBuilder.Append(args.TextSpan.iStartIndex);
            outputMessageBuilder.Append("):");
            outputMessageBuilder.Append("error ");
            
            if (args.Exception != null)
            {
                outputMessageBuilder.Append(args.Message);
            }
            else
            {
                outputMessageBuilder.Append("SQL");
                outputMessageBuilder.Append(args.Error.Number);
                outputMessageBuilder.Append(": ");
                outputMessageBuilder.Append(args.Error.Message);
            }
            
            return outputMessageBuilder.ToString();
        }

        public static string Format(this BatchParserExecutionErrorEventArgs args, string source)
        {
            ArgumentNullException.ThrowIfNull(args);

            var outputMessageBuilder = new StringBuilder();
            outputMessageBuilder.Append(source);
            outputMessageBuilder.Append('(');
            outputMessageBuilder.Append(args.Line);
            outputMessageBuilder.Append(',');
            outputMessageBuilder.Append(args.TextSpan.iStartIndex);
            outputMessageBuilder.Append("): ");
            outputMessageBuilder.Append("error: ");
            
            if (args.Exception != null)
            {
                outputMessageBuilder.Append(args.Message);
            }
            else if (args.Error != null)
            {
                outputMessageBuilder.Append("SQL");
                outputMessageBuilder.Append(args.Error.Number);
                outputMessageBuilder.Append(": ");
                outputMessageBuilder.Append(args.Error.Message);
            }
            else
            {
                outputMessageBuilder.Append(args.Message);
                outputMessageBuilder.Append(' ');
                outputMessageBuilder.Append(args.Description);
            }
            
            return outputMessageBuilder.ToString();
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
                stringBuilder.Append(modelError.Message);
            }

            return stringBuilder.ToString();
        }

        public static string GetPreDeploymentScript(this DacPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            var stream = package.PreDeploymentScript;
            if (stream == null)
            {
                return null;
            }

            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }

        public static string GetPostDeploymentScript(this DacPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            var stream = package.PostDeploymentScript;
            if (stream == null)
            {
                return null;
            }

            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }

        public static void AddReference(this TSqlModel model, string referencePath, string externalParts, bool suppressErrorsForMissingDependencies)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(referencePath);

            var dataSchemaModel = GetDataSchemaModel(model);

            var crossPlatformPath = referencePath.Replace('\\', '/');

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

        private static string ParseExternalParts(string externalParts)
        {
            string serverVariableName = null;
            string databaseVariableName = null;
            string databaseVariableLiteralValue = null;

            // If there are '=' sign in argument assumes that this is formula, else assume that a single value passed and that it is database literal.
            if (externalParts.Contains('=', StringComparison.OrdinalIgnoreCase))
            {
                foreach (Match match in new Regex(@"dbl=(?<dbl>\w+)|dbv=(?<dbv>\w+)|srv=(?<srv>\w+)",
                    RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Matches(externalParts))
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
                var method = Type.GetType("Microsoft.Data.Tools.Schema.Common.FileUtils, Microsoft.Data.Tools.Utilities");

                if (method == null)
                {
                    throw new InvalidOperationException("Unable to load Microsoft.Data.Tools.Utilities assembly.");
                }

                _ensureIsDelimitedMethod = method
                    .GetMethod("EnsureIsDelimited", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, new[]{ typeof(string) }, null);
            }

            if (_ensureIsDelimitedMethod == null)
            {
                throw new InvalidOperationException("Unable to find EnsureIsDelimited method in Microsoft.Data.Tools.Utilities assembly.");
            }

            return (string)_ensureIsDelimitedMethod.Invoke(null, new object[]{ name });
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

                var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);

                if (setMetadataMethod == null)
                {
                    throw new InvalidOperationException("Unable to find SetMetadata method on CustomSchemaData.");
                }

                setMetadataMethod.Invoke(customData, new object[] { variableName, string.Empty });
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

        private static void AddCustomData(object dataSchemaModel, object customData)
        {
            var addCustomDataMethod = dataSchemaModel.GetType().GetMethod("AddCustomData", BindingFlags.Public | BindingFlags.Instance);

            if (addCustomDataMethod == null)
            {
                throw new InvalidOperationException("Unable to find AddCustomData method on DataSchemaModel.");
            }

            addCustomDataMethod.Invoke(dataSchemaModel, new[] { customData });
        }
    }
}
