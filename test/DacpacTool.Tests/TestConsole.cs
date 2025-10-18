using System;
using System.Collections.Generic;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests;

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
