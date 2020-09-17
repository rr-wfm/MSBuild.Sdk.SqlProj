using System;
using System.IO;
using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class ScriptInspector
    {
        private readonly List<string> _includedFiles = new List<string>();
        public IEnumerable<string> IncludedFiles
        {
            get => _includedFiles;
        }

        public void AddPreDeploymentScript(FileInfo script)
        {
            AddIncludedFiles(script);
        }

        public void AddPostDeploymentScript(FileInfo script)
        {
            AddIncludedFiles(script);
        }

        private void AddIncludedFiles(FileInfo file)
        {
            var parser = new ScriptParser(file.FullName, new IncludeVariableResolver());
            _includedFiles.AddRange(parser.CollectFileNames());
        }
    }
}
