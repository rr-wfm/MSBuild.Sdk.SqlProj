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
        public static string GetOutputMessage(this SqlRuleProblem sqlRuleProblem, HashSet<string> errorRules)
        {
            ArgumentNullException.ThrowIfNull(sqlRuleProblem);

            SqlRuleProblemSeverity sqlRuleProblemSeverity = sqlRuleProblem.Severity;

            if (errorRules.Contains(sqlRuleProblem.RuleId))
            {
                sqlRuleProblemSeverity = SqlRuleProblemSeverity.Error;
            }

            // Wildcard suppression entries are configured like "SRD*" and matched as prefix matches.
            // This span-based prefix check is equivalent to checking rule[..^1] but avoids per-call
            // substring allocations while iterating potentially many diagnostics.
            foreach (var rule in errorRules)
            {
                if (rule.EndsWith('*') &&
                    sqlRuleProblem.RuleId.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase))
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
