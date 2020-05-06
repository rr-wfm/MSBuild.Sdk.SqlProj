using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MSBuild.Sdk.SqlProj.BuildDacpac
{
    public class SqlCmdVariableResolver
    {
        private const string TypeName = "Microsoft.Data.Tools.Schema.Sql.Deployment.SqlCmdVariableResolver, Microsoft.Data.Tools.Schema.Sql";
        private readonly object _Instance;

        public SqlCmdVariableResolver(IDictionary<string, string> predefinedVariables, string ambientVariablesFormat)
        {
            _Instance = Activator.CreateInstance(Type.GetType(TypeName),
                                                 BindingFlags.Instance | BindingFlags.NonPublic,
                                                 null,
                                                 new object[] { predefinedVariables, ambientVariablesFormat },
                                                 CultureInfo.InvariantCulture);
        }

        public object Instance { get { return _Instance; }}
    }
}