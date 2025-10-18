namespace MSBuild.Sdk.SqlProj.DacpacTool;

public interface IConsole
{
    void WriteLine(string value);
    string ReadLine();
}
