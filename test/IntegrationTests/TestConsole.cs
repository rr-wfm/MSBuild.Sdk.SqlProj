using MSBuild.Sdk.SqlProj.DacpacTool;

namespace MSBuild.Sdk.SqlProj.IntegrationTests
{
    internal class TestConsole : IConsole
    {
        public readonly List<string> Lines = new List<string>();

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
    }
}
