using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// Invokes dotnet directly: project settings are arguments, never shell commands.
public sealed class SqlProjRunDacpacTool : Task
{
    [Required]
    public string ToolPath { get; set; }

    [Required]
    public string Command { get; set; }

    [Required]
    public string WorkingDirectory { get; set; }

    public ITaskItem[] Arguments { get; set; }

    public override bool Execute()
    {
        try
        {
            var arguments = new StringBuilder(Quote(ToolPath)).Append(' ').Append(Quote(Command));
            foreach (var argument in Arguments ?? new ITaskItem[0])
            {
                arguments.Append(' ').Append(Quote(argument.ItemSpec));
                if (!string.Equals(argument.GetMetadata("Switch"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    arguments.Append(' ').Append(Quote(argument.GetMetadata("Value")));
                }
            }

            var start = new ProcessStartInfo("dotnet", arguments.ToString())
            {
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = new Process { StartInfo = start })
            {
                DataReceivedEventHandler report = (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Log.LogMessageFromText(args.Data, MessageImportance.High);
                    }
                };
                process.OutputDataReceived += report;
                process.ErrorDataReceived += report;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                // Stream output so interactive password prompts are visible.
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Log.LogError("DacpacTool {0} failed with exit code {1}.", Command, process.ExitCode);
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError("Could not run DacpacTool: {0}", ex.Message);
        }
        return !Log.HasLoggedErrors;
    }

    // ProcessStartInfo.Arguments uses double-quote/backslash escaping on Windows
    // and dotnet on Unix. No command shell participates in parsing these values.
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value ?? string.Empty)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }
            result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
            result.Append(character);
            backslashes = 0;
        }
        return result.Append('\\', backslashes * 2).Append('"').ToString();
    }
}
