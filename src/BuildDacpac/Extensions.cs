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
            var service = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
            var dataSchemaModel = service.GetType().GetProperty("DataSchemaModel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);

            var customData = Activator.CreateInstance(Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql"), "Reference", "SqlSchema");
            var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);
            setMetadataMethod.Invoke(customData, new object[] { "FileName", referencePath });
            setMetadataMethod.Invoke(customData, new object[] { "LogicalName", Path.GetFileName(referencePath) });
            setMetadataMethod.Invoke(customData, new object[] { "SuppressMissingDependenciesErrors", "False" });

            var addCustomDataMethod = dataSchemaModel.GetType().GetMethod("AddCustomData", BindingFlags.Public | BindingFlags.Instance);
            addCustomDataMethod.Invoke(dataSchemaModel, new object[] { customData });
        }

        public static void AddSqlCmdVariable(this TSqlModel model, string variableName)
        {
            var service = model.GetType().GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
            var dataSchemaModel = service.GetType().GetProperty("DataSchemaModel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);

            var customData = Activator.CreateInstance(Type.GetType("Microsoft.Data.Tools.Schema.SchemaModel.CustomSchemaData, Microsoft.Data.Tools.Schema.Sql"), "SqlCmdVariables", "SqlCmdVariable");
            var setMetadataMethod = customData.GetType().GetMethod("SetMetadata", BindingFlags.Public | BindingFlags.Instance);
            setMetadataMethod.Invoke(customData, new object[] { variableName, string.Empty });

            var addCustomDataMethod = dataSchemaModel.GetType().GetMethod("AddCustomData", BindingFlags.Public | BindingFlags.Instance);
            addCustomDataMethod.Invoke(dataSchemaModel, new object[] { customData });
        }
    }
}
