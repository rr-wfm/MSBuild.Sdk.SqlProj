using System;
using System.IO;
using System.Reflection;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.BuildDacpac
{
    public static class Extensions
    {
        public static void AddReference(this TSqlModel model, string referencePath)
        {
            var dataSchemaModel = GetDataSchemaModel(model);

            var customData = Activator.CreateInstance(Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql"), "Reference", "SqlSchema");
            var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);
            setMetadataMethod.Invoke(customData, new object[] { "FileName", referencePath });
            setMetadataMethod.Invoke(customData, new object[] { "LogicalName", Path.GetFileName(referencePath) });
            setMetadataMethod.Invoke(customData, new object[] { "SuppressMissingDependenciesErrors", "False" });

            AddCustomData(dataSchemaModel, customData);
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
