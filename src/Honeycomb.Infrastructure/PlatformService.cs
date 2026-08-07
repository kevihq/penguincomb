using Honeycomb.Application.Abstractions;
using System.Runtime.InteropServices;

namespace Honeycomb.Infrastructure;

/// <summary>Cross-platform OS details backed by .NET runtime checks.</summary>
public class PlatformService : IPlatformService
{
    public string OsKind => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "macOS";
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsLinux => OperatingSystem.IsLinux();
    public string UserName => Environment.UserName;

    public string GetEnvironmentVariable(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    public IReadOnlyList<string> GetPathDirectories()
    {
        string path = GetEnvironmentVariable("PATH", "");
        char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        return path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }
}
