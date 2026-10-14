using System.Diagnostics;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.IntegrationTests;

/// <summary>
/// Runs an external process for integration tests, capturing output, enforcing a timeout,
/// and asserting a successful exit code.
/// </summary>
internal static class ProcessRunner
{
    public static async Task RunAsync(
        string workingDirectory,
        string executable,
        IReadOnlyList<string> arguments,
        Action<string> writeOutput,
        CancellationToken cancellationToken,
        Func<string, string>? sanitizeOutput = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{executable} exceeded the ten-minute test timeout.");
        }
        finally
        {
            var output = (await stdout) + (await stderr);
            writeOutput(sanitizeOutput?.Invoke(output) ?? output);
        }

        process.ExitCode.ShouldBe(0, $"{executable} failed. See test output.");
    }
}
