using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MSBuild.Sdk.SqlProj.BuildDacpac
{
    /// <summary>
    /// Resolves included file names from a SQLCMD script file.
    /// </summary>
    public class IncludeFileNamesCollector
    {
        private const string TypeName = "Microsoft.Data.Tools.Schema.Sql.Deployment.IncludeFileNamesCollector, Microsoft.Data.Tools.Schema.Sql";
        private static readonly Type RuntimeType = Type.GetType(TypeName);
        private readonly object _Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncludeFileNamesCollector" /> class.
        /// </summary>
        /// <param name="path">Path to a file to start parsing.</param>
        /// <param name="sqlCmdVariableResolver">A <see cref="SqlCmdVariableResolver" /> instance used to resolve SQLCMD variables.</param>
        public IncludeFileNamesCollector(string path, SqlCmdVariableResolver sqlCmdVariableResolver)
        {
            _Instance = Activator.CreateInstance(RuntimeType, BindingFlags.Instance | BindingFlags.Public, null, new object[] { path, sqlCmdVariableResolver.Instance }, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Collects the file names of files included from the start script.
        /// </summary>
        /// <returns>Returns a range of strings representing the filenames of included files.</returns>
        public IEnumerable<string> CollectIncludedFilenames()
        {
            var includeFileNamesMethod = RuntimeType.GetMethod("CollectIncludedFilenames", BindingFlags.Instance | BindingFlags.NonPublic);
            var includeFileNames = includeFileNamesMethod.Invoke(_Instance, null) as IEnumerable<string>;
            return includeFileNames;
        }

        /// <summary>
        /// Gets any parse errors that occured while parsing the SQLCMD files.
        /// </summary>
        /// <returns>Returns a range of strings representing the parse errors that occured.</returns>
        public IEnumerable<string> GetParseErrors()
        {
            var getParseErrorsMethod = RuntimeType.GetMethod("GetParseErrors", BindingFlags.Instance | BindingFlags.Public);
            var parseErrors = getParseErrorsMethod.Invoke(_Instance, null) as IEnumerable<object>;
            return parseErrors.Select(error => error.ToString()).ToList();
        }
    }
}