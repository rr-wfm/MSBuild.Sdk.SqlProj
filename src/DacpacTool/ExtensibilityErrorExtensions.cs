using System;
using System.Text;
using Microsoft.SqlServer.Dac.Extensibility;

namespace MSBuild.Sdk.SqlProj.DacpacTool;

/// <summary>
/// A wrapper for <see cref="ExtensibilityError" /> that provides MSBuild compatible output and source document information.
/// </summary>
public static class ExtensibilityErrorExtensions
{
    public static string GetOutputMessage(this ExtensibilityError extensibilityError)
    {
        ArgumentNullException.ThrowIfNull(extensibilityError);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append(extensibilityError.Document);
        stringBuilder.Append('(');
        stringBuilder.Append(extensibilityError.Line);
        stringBuilder.Append(',');
        stringBuilder.Append(extensibilityError.Column);
        stringBuilder.Append("):");
        stringBuilder.Append(' ');
        stringBuilder.Append(extensibilityError.Severity);
        stringBuilder.Append(' ');
        stringBuilder.Append(extensibilityError.ErrorCode);
        stringBuilder.Append(": ");
        stringBuilder.Append(extensibilityError.Message);
        
        return stringBuilder.ToString();
    }
}
