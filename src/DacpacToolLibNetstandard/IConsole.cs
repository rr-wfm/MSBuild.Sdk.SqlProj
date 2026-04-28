namespace MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

public interface IConsole
{
    void WriteLine(string value);
    string ReadLine();
}