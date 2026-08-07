namespace Honeycomb.Application.Abstractions;

/// <summary>
/// Abstracts OS-level platform details so shared code never depends on
/// Windows-specific APIs (registry, environment, executables, paths).
/// </summary>
public interface IPlatformService
{
    /// <summary>"Windows", "Linux" or "macOS".</summary>
    string OsKind { get; }

    bool IsWindows { get; }

    bool IsLinux { get; }

    /// <summary>The current user's account name (used for the chart author default).</summary>
    string UserName { get; }

    /// <summary>Returns the value of an environment variable, or <paramref name="fallback"/>.</summary>
    string GetEnvironmentVariable(string name, string fallback);

    /// <summary>Returns the directories listed in the PATH environment variable.</summary>
    IReadOnlyList<string> GetPathDirectories();
}
