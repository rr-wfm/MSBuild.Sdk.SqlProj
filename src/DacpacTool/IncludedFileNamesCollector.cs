using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlTools.ServiceLayer.BatchParser;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class IncludedFileNamesCollector : ICommandHandler
    {
        private readonly Parser _parser;
        private readonly List<string> _includedFileNames = new List<string>();
        private string _currentFilePath;

        public IncludedFileNamesCollector(string sourceFile, IVariableResolver variableResolver)
        {
            if (Path.IsPathRooted(sourceFile))
            {
                _currentFilePath = Path.GetDirectoryName(sourceFile);
            }
            else
            {
                sourceFile = Path.Combine(Environment.CurrentDirectory, sourceFile);
                _currentFilePath = Path.GetDirectoryName(sourceFile);
            }

            _parser = new Parser(this, variableResolver, new StreamReader(sourceFile), null);
        }

        public string[] CollectFileNames()
        {
            _parser.Parse();
            return _includedFileNames.ToArray();
        }

        BatchParserAction ICommandHandler.Go(TextBlock batch, int repeatCount, SqlCmdCommand tokenType)
        {
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.Include(TextBlock filename, out TextReader stream, out string newFilename)
        {
            filename.GetText(true, out string includedFileName, out LineInfo lineInfo);

            if (!Path.IsPathRooted(includedFileName))
            {
                includedFileName = Path.Combine(_currentFilePath, includedFileName);
            }

            _currentFilePath = Path.GetDirectoryName(includedFileName);
            _includedFileNames.Add(includedFileName);

            stream = new StreamReader(includedFileName);
            newFilename = includedFileName;
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.OnError(Token token, OnErrorAction action)
        {
            return BatchParserAction.Continue;
        }
    }
}
