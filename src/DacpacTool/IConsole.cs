namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public interface IConsole
    {
        void WriteLine(string value);
        void WriteVerboseLine(string value);
        string ReadLine();
    }
}
