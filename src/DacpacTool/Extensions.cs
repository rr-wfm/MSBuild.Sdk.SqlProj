using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public static class Extensions
    {
        public static string Format(this BatchErrorEventArgs args, string source)
        {
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
            else if (args.Error != null)
            {
                outputMessageBuilder.Append("SQL");
                outputMessageBuilder.Append(args.Error.Number);
                outputMessageBuilder.Append(": ");
                outputMessageBuilder.Append(args.Error.Message);
            }
            else
            {
                outputMessageBuilder.Append(args.MessageType);
                outputMessageBuilder.Append(' ');
                outputMessageBuilder.Append(args.Message);
                outputMessageBuilder.Append(' ');
                outputMessageBuilder.Append(args.Description);
            }
            
            return outputMessageBuilder.ToString();
        }

        public static string GetPreDeploymentScript(this DacPackage package)
        {
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
            var stream = package.PostDeploymentScript;
            if (stream == null)
            {
                return null;
            }

            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }

        public static void AddReference(this TSqlModel model, string referencePath, string externalParts)
        {
            var dataSchemaModel = GetDataSchemaModel(model);

            var customData = Activator.CreateInstance(Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql"), "Reference", "SqlSchema");
            var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);
            setMetadataMethod.Invoke(customData, new object[] { "FileName", referencePath });
            setMetadataMethod.Invoke(customData, new object[] { "LogicalName", Path.GetFileName(referencePath) });
            setMetadataMethod.Invoke(customData, new object[] { "SuppressMissingDependenciesErrors", "False" });

            if (!string.IsNullOrWhiteSpace(externalParts))
            {
                setMetadataMethod.Invoke(customData, new object[] { "ExternalParts", Identifier.EncodeIdentifier(externalParts) });
            }

            AddCustomData(dataSchemaModel, customData);
        }

        public static IEnumerable<string> GetReferencedDacPackages(this TSqlModel model)
        {
            var result = new List<string>();
            var dataSchemaModel = GetDataSchemaModel(model);

            var getCustomDataMethod = dataSchemaModel.GetType().GetMethod("GetCustomData", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(string) }, null);
            var references = (IEnumerable) getCustomDataMethod.Invoke(dataSchemaModel, new object[] { "Reference", "SqlSchema" });

            MethodInfo getMetadataMethod = null;
            foreach (var reference in references)
            {
                if (getMetadataMethod == null)
                {
                    getMetadataMethod = reference.GetType().GetMethod("GetMetadata", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                }

                var fileName = (string)getMetadataMethod.Invoke(reference, new object[] { "FileName" });
                result.Add(fileName);
            }

            return result;
        }

        public static void AddSqlCmdVariables(this TSqlModel model, string[] variableNames)
        {
            var dataSchemaModel = GetDataSchemaModel(model);

            var customData = Activator.CreateInstance(Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql"), "SqlCmdVariables", "SqlCmdVariable");

            foreach (var variableName in variableNames)
            {
                var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);
                setMetadataMethod.Invoke(customData, new object[] { variableName, string.Empty });
            }

            AddCustomData(dataSchemaModel, customData);
        }

        public static IEnumerable<ModelValidationError> GetModelValidationErrors(this TSqlModel model, IEnumerable<string> ignoreValidationErrrors)
        {
            var service = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
            var getModelValidationErrorsMethod = service.GetType().GetMethod("GetModelValidationErrors", BindingFlags.NonPublic | BindingFlags.Instance);
            var modelValidationErrors = getModelValidationErrorsMethod.Invoke(service, new object[] { ignoreValidationErrrors }) as IEnumerable<object>;

            var createDacModelErrorMethod = service.GetType().GetMethod("CreateDacModelError", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = new List<ModelValidationError>();
            PropertyInfo documentProperty = null;
            foreach (var modelValidationError in modelValidationErrors)
            {
                if (documentProperty == null)
                {
                    documentProperty = modelValidationError.GetType().GetProperty("Document", BindingFlags.Public | BindingFlags.Instance);
                }

                var dacModelError = createDacModelErrorMethod.Invoke(service, new object[] { modelValidationError }) as DacModelError;
                result.Add(new ModelValidationError(dacModelError, documentProperty.GetValue(modelValidationError) as string));
            }

            return result;
        }

        private static object GetDataSchemaModel(TSqlModel model)
        {
            var service = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
            var dataSchemaModel = service.GetType().GetProperty("DataSchemaModel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);
            return dataSchemaModel;
        }

        private static void AddCustomData(object dataSchemaModel, object customData)
        {
            var addCustomDataMethod = dataSchemaModel.GetType().GetMethod("AddCustomData", BindingFlags.Public | BindingFlags.Instance);
            addCustomDataMethod.Invoke(dataSchemaModel, new object[] { customData });
        }
    }
}
