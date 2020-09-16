using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class ScriptParser : ICommandHandler
    {
        private readonly Parser _parser;
        private bool _parsed;
        private readonly List<string> _includedFileNames = new List<string>();
        private readonly StringBuilder _scriptBuilder = new StringBuilder();
        private const string BATCH_SEPARATOR = "GO";

        public ScriptParser(string sourceFile, IVariableResolver variableResolver)
        {
            _parser = new Parser(this, variableResolver, new StreamReader(sourceFile), sourceFile);
        }

        private void Parse()
        {
            if (! _parsed)
            {
                _parser.Parse();
                _parsed = true;
            }
        }

        public IEnumerable<string> CollectFileNames()
        {
            Parse();
            return _includedFileNames;
        }

        public string GenerateScript()
        {
            Parse();
            return _scriptBuilder.ToString();
        }

        BatchParserAction ICommandHandler.Go(TextBlock batch, int repeatCount, SqlCmdCommand tokenType)
        {
            batch.GetText(true, out string batchText, out LineInfo lineInfo);
            _scriptBuilder.AppendLine(batchText);
            _scriptBuilder.AppendLine(BATCH_SEPARATOR);
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.Include(TextBlock includeBlock, out TextReader stream, out string newFilename)
        {
            includeBlock.GetText(true, out string includedFileName, out LineInfo lineInfo);

            if (!Path.IsPathRooted(includedFileName))
            {
                var position = lineInfo.GetStreamPositionForOffset(0);
                includedFileName = Path.Combine(Path.GetDirectoryName(position.Filename), includedFileName);
            }

            _includedFileNames.Add(includedFileName);

            stream = new StreamReader(includedFileName);
            newFilename = includedFileName;
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.OnError(Token token, OnErrorAction action)
        {
            // Write error to console - based on ModelValidationError
            // Error SQL9000001 is custom to this project, 
            // not a known MSBuild error code
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(Path.GetFileNameWithoutExtension(token.Filename)); // SourceName
            stringBuilder.Append('(');
            stringBuilder.Append(token.Begin.Line); // Line
            stringBuilder.Append(',');
            stringBuilder.Append(token.Begin.Column); // Column
            stringBuilder.Append("):");
            stringBuilder.Append(ModelErrorType.ParserError); // ErrorType
            stringBuilder.Append(' ');
            stringBuilder.Append(ModelErrorSeverity.Error); // Severity
            stringBuilder.Append(' ');
            stringBuilder.Append("SQL"); // Prefix
            stringBuilder.Append(9000001); // ErrorCode
            stringBuilder.Append($": Parser error in {Path.GetFileName(token.Filename)}"); // Message
            Console.Error.WriteLine(stringBuilder.ToString());

            return action == OnErrorAction.Ignore ? BatchParserAction.Continue : BatchParserAction.Abort;
        }
    }
}
