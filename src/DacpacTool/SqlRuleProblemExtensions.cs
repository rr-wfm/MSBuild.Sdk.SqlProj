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
    public static class SqlRuleProblemExtensions
    {
        public static string GetOutputMessage(this SqlRuleProblem sqlRuleProblem, HashSet<string> errorRules)
        {
            ArgumentNullException.ThrowIfNull(sqlRuleProblem);

            SqlRuleProblemSeverity sqlRuleProblemSeverity = sqlRuleProblem.Severity;

            if (errorRules.Contains(sqlRuleProblem.RuleId))
            {
                sqlRuleProblemSeverity = SqlRuleProblemSeverity.Error;
            }

            var wildCardErrorRules = errorRules
                .Where(r => r.EndsWith("*", StringComparison.OrdinalIgnoreCase));
            if (wildCardErrorRules.Any(s => sqlRuleProblem.RuleId.StartsWith(s[..^1])))
            {
                sqlRuleProblemSeverity = SqlRuleProblemSeverity.Error;
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
