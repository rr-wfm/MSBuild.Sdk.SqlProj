using System;
using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    internal class TestConsole : IConsole
    {
        private readonly bool _verbose;
        public readonly List<string> Lines = new List<string>();

        public TestConsole(bool verbose = true)
        {
            _verbose = verbose;
        }

        public string ReadLine()
        {
            throw new NotImplementedException();
        }

        public void WriteLine(string value)
        {
            var values = value.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var line in values)
            {
                Lines.Add(line);
            }
        }

        public void WriteVerboseLine(string value)
        {
            if (_verbose)
            {
                WriteLine(value);
            }
        }
    }
}
