namespace PenguinComb.Application.Abstractions;

/// <summary>The result of running an external process.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Safe cross-platform external-process runner. Implementations must use
/// <c>ProcessStartInfo.ArgumentList</c>, capture stdout/stderr without deadlocking,
/// support cancellation, and never assemble shell command strings.
/// </summary>
public interface IExternalProcessService
{
    /// <summary>
    /// Runs <paramref name="executable"/> with the given arguments.
    /// Returns the combined result; throws <see cref="OperationCanceledException"/> on cancellation.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an executable by name on the PATH (and on Linux verifies it is executable).
    /// Returns null when not found.
    /// </summary>
    Task<string?> FindExecutableOnPathAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches an executable with shell-execute semantics (used for Windows elevation
    /// prompts only). The single argument is passed as one quoted shell argument.
    /// Throws <see cref="PlatformNotSupportedException"/> on non-Windows platforms.
    /// </summary>
    Task RunElevatedAsync(string executable, string argument, CancellationToken cancellationToken = default);
}
