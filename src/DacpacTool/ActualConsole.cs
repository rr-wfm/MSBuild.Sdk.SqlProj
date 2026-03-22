using System;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class ActualConsole : IConsole
    {
        private readonly bool _verbose;

        public ActualConsole(bool verbose = false)
        {
            _verbose = verbose;
        }

        public string ReadLine()
        {
            return Console.ReadLine();
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        public void WriteVerboseLine(string value)
        {
            if (_verbose)
            {
                Console.WriteLine(value);
            }
        }
    }
}
