using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class ScriptParser : ICommandHandler, IDisposable
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
                try
                {
                    _parser.Parse();
                    _parsed = true;
                }
                catch (BatchParserException ex)
                {
                    string mostRecentFile = _includedFileNames.LastOrDefault();
                    throw new InvalidOperationException(
                        mostRecentFile == null ? 
                            ex.Message : 
                            $"{ex.Message} File: {Path.GetFileName(mostRecentFile)}"
                    );
                }
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

                var projectDir = Path.GetDirectoryName(position.Filename);

                if (string.IsNullOrEmpty(projectDir))
                {
                    throw new InvalidOperationException($"Unable to resolve path for included file '{includedFileName}'");
                }

                includedFileName = Path.Combine(projectDir, includedFileName);
            }

            _includedFileNames.Add(includedFileName);

            stream = new StreamReader(includedFileName);
            newFilename = includedFileName;
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.OnError(Token token, OnErrorAction action)
        {
            // Write error to console
            var error = new ModelValidationError(
                sourceName: Path.GetFileName(token.Filename),
                line: token.Begin.Line,
                column: token.Begin.Column,
                errorType: ModelErrorType.ParserError,
                severity: ModelErrorSeverity.Error,
                prefix: "SQL",
                errorCode: 9000001, // custom to this project, not a known MSBuild error code
                message: $": Parser error in {Path.GetFileName(token.Filename)}"
            );
            Console.Error.WriteLine(error.ToString());

            return action == OnErrorAction.Ignore ? BatchParserAction.Continue : BatchParserAction.Abort;
        }

        public void Dispose()
        {
            _parser?.Dispose();
        }
    }
}
