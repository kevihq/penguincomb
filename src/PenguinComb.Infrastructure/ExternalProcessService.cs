using System.Diagnostics;
using System.Text;
using PenguinComb.Application.Abstractions;

namespace PenguinComb.Infrastructure;

/// <summary>
/// Safe cross-platform process runner. Uses <c>ProcessStartInfo.ArgumentList</c>
/// (no shell quoting), captures stdout/stderr asynchronously (no deadlocks), supports
/// cancellation, and reports non-zero exit codes.
/// </summary>
public class ExternalProcessService : IExternalProcessService
{
    private readonly IPlatformService _platform;

    public ExternalProcessService(IPlatformService platform)
    {
        _platform = platform;
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Process already exited
                }
            }
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    public Task<string?> FindExecutableOnPathAsync(string name, CancellationToken cancellationToken = default)
    {
        string[] candidates = _platform.IsWindows
            ? [name, $"{name}.exe", $"{name}.cmd"]
            : [name];

        foreach (string dir in _platform.GetPathDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            foreach (string candidate in candidates)
            {
                string fullPath = Path.Combine(dir, candidate);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                if (_platform.IsWindows)
                {
                    return Task.FromResult<string?>(fullPath);
                }

                // On Linux the executable bit must be set.
                if (IsExecutable(fullPath))
                {
                    return Task.FromResult<string?>(fullPath);
                }
            }
        }

        return Task.FromResult<string?>(null);
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & UnixFileMode.UserExecute) != 0 ||
                   (mode & UnixFileMode.GroupExecute) != 0 ||
                   (mode & UnixFileMode.OtherExecute) != 0;
        }
        catch
        {
            return false;
        }
    }

    public Task RunElevatedAsync(string executable, string argument, CancellationToken cancellationToken = default)
    {
        if (!_platform.IsWindows)
        {
            throw new PlatformNotSupportedException("Elevated shell execution is only supported on Windows.");
        }

        // Windows-only: shell-execute triggers the UAC elevation prompt. A single
        // quoted argument is safe here because it is a user-selected folder path.
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            CreateNoWindow = false,
            Arguments = $"\"{argument.Replace("\"", "\\\"")}\"",
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();
        return Task.CompletedTask;
    }
}
