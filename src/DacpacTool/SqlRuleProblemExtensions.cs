using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Dac.CodeAnalysis;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// A wrapper for <see cref="SqlRuleProblem" /> that provides MSBuild compatible output and source document information.
    /// </summary>
    internal static class SqlRuleProblemExtensions
    {
        public static string GetOutputMessage(this SqlRuleProblem sqlRuleProblem, HashSet<string> errorRules, HashSet<string> errorRulePrefixes)
        {
            ArgumentNullException.ThrowIfNull(sqlRuleProblem);
            ArgumentNullException.ThrowIfNull(errorRulePrefixes);

            SqlRuleProblemSeverity sqlRuleProblemSeverity = sqlRuleProblem.Severity;

            if (errorRules.Contains(sqlRuleProblem.RuleId))
            {
                sqlRuleProblemSeverity = SqlRuleProblemSeverity.Error;
            }

            // Wildcard severity overrides are stored as the full rule prefix from the
            // rules expression (for "+!SqlServer.Rules.SRD*", we store
            // "SqlServer.Rules.SRD"), so we only keep this loop for prefix rules.
            foreach (var rule in errorRulePrefixes)
            {
                if (sqlRuleProblem.RuleId.StartsWith(rule, StringComparison.OrdinalIgnoreCase))
                {
                    sqlRuleProblemSeverity = SqlRuleProblemSeverity.Error;
                    break;
                }
            }
            
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(sqlRuleProblem.SourceName);
            stringBuilder.Append('(');
            stringBuilder.Append(sqlRuleProblem.StartLine);
            stringBuilder.Append(',');
            stringBuilder.Append(sqlRuleProblem.StartColumn);
            stringBuilder.Append("):");
            stringBuilder.Append(' ');
            stringBuilder.Append(sqlRuleProblemSeverity);
            stringBuilder.Append(' ');
            stringBuilder.Append(sqlRuleProblem.ErrorMessageString);
            
            return stringBuilder.ToString();
        }
    }
}
