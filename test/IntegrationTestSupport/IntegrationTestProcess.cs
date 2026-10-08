using System.Diagnostics;
using DotNet.Testcontainers.Containers;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.TestSupport;

/// <summary>
/// Runs test commands and captures diagnostics with the database password redacted.
/// </summary>
internal sealed class IntegrationTestProcess
{
    private readonly TestContext _context;
    private readonly string _workingDirectory;
    private readonly string _password;
    private readonly string? _diagnosticsDirectory;
    private readonly string? _packageCache;
    private int _processNumber;

    public IntegrationTestProcess(TestContext context, string workingDirectory, string password,
        string? diagnosticsDirectory = null, string? packageCache = null)
    {
        _context = context;
        _workingDirectory = workingDirectory;
        _password = password;
        _diagnosticsDirectory = diagnosticsDirectory;
        _packageCache = packageCache;
    }

    /// <summary>
    /// Runs a command with a ten-minute timeout, records its output, and asserts a successful exit.
    /// </summary>
    public async Task RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (_packageCache != null)
        {
            start.Environment["NUGET_PACKAGES"] = _packageCache;
        }
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
            var output = Redact((await stdout) + (await stderr));
            _context.WriteLine(output);
            if (_diagnosticsDirectory != null)
            {
                await File.WriteAllTextAsync(Path.Combine(_diagnosticsDirectory, $"process-{++_processNumber}.log"), output, CancellationToken.None);
            }
        }
        process.ExitCode.ShouldBe(0, $"{executable} failed. See test output.");
    }

    /// <summary>
    /// Captures container logs to a diagnostic file or test output without failing cleanup if capture fails.
    /// </summary>
    public async Task CaptureLogsAsync(IContainer container, string name)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var (stdout, stderr) = await container.GetLogsAsync(ct: timeout.Token);
            var output = Redact(stdout + stderr);
            if (_diagnosticsDirectory != null)
            {
                await File.WriteAllTextAsync(Path.Combine(_diagnosticsDirectory, name), output, timeout.Token);
            }
            else
            {
                _context.WriteLine(output);
            }
        }
        catch (Exception exception)
        {
            _context.WriteLine(Redact($"Could not capture {name}: {exception.Message}"));
        }
    }

    private string Redact(string value) => value.Replace(_password, "[REDACTED]", StringComparison.Ordinal);
}
