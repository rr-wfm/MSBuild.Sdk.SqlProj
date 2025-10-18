using System;

namespace MSBuild.Sdk.SqlProj.DacpacTool;

public class ActualConsole : IConsole
{
    public string ReadLine()
    {
        return Console.ReadLine();
    }

    public void WriteLine(string value)
    {
        Console.WriteLine(value);
    }
}
