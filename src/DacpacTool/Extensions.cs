using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
#pragma warning disable CA1724 // Type names should not match namespaces
    public static class Extensions
#pragma warning restore CA1724 // Type names should not match namespaces
    {

        public static string Format(this BatchErrorEventArgs args, string source)
        {
            ArgumentNullException.ThrowIfNull(args);

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
            ArgumentNullException.ThrowIfNull(args);

            var outputMessageBuilder = new StringBuilder();
            outputMessageBuilder.Append(source);
            outputMessageBuilder.Append('(');
            outputMessageBuilder.Append(args.Line);
            outputMessageBuilder.Append(',');
            outputMessageBuilder.Append(args.TextSpan.iStartIndex);
            outputMessageBuilder.Append("): ");
            outputMessageBuilder.Append("error: ");
            
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
                outputMessageBuilder.Append(args.Message);
                outputMessageBuilder.Append(' ');
                outputMessageBuilder.Append(args.Description);
            }
            
            return outputMessageBuilder.ToString();
        }

        public static string GetPreDeploymentScript(this DacPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

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
            ArgumentNullException.ThrowIfNull(package);

            var stream = package.PostDeploymentScript;
            if (stream == null)
            {
                return null;
            }

            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }
}
