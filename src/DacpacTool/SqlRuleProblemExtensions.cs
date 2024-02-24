using System;
using System.Text;
using Microsoft.SqlServer.Dac.CodeAnalysis;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// A wrapper for <see cref="SqlRuleProblem" /> that provides MSBuild compatible output and source document information.
    /// </summary>
    public static class SqlRuleProblemExtensions
    {
        public static string GetOutputMessage(this SqlRuleProblem sqlRuleProblem)
        {
            ArgumentNullException.ThrowIfNull(sqlRuleProblem);
            
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(sqlRuleProblem.SourceName ?? sqlRuleProblem.ModelElement.Name.ToString());
            stringBuilder.Append('(');
            stringBuilder.Append(sqlRuleProblem.StartLine);
            stringBuilder.Append(',');
            stringBuilder.Append(sqlRuleProblem.StartColumn);
            stringBuilder.Append("):");
            stringBuilder.Append(' ');
            stringBuilder.Append(sqlRuleProblem.Severity);
            stringBuilder.Append(' ');
            stringBuilder.Append(sqlRuleProblem.ErrorMessageString);
            
            return stringBuilder.ToString();
        }
    }
}
