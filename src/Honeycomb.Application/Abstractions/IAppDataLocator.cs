namespace Honeycomb.Application.Abstractions;

/// <summary>
/// Per-user writable application directories (XDG on Linux, %APPDATA%/%LOCALAPPDATA%
/// on Windows). All generated data, backups, logs and settings live here - never beside
/// the application executable.
/// </summary>
public interface IAppDataLocator
{
    /// <summary>Configuration directory (settings.json).</summary>
    string ConfigDirectory { get; }

    /// <summary>Writable application data (backups, templates, logs).</summary>
    string DataDirectory { get; }

    /// <summary>Cache/temporary directory.</summary>
    string CacheDirectory { get; }

    /// <summary>Directory for game-file backups (per game subfolders).</summary>
    string BackupsDirectory { get; }

    /// <summary>Directory for the default .ghproj template.</summary>
    string TemplatesDirectory { get; }

    /// <summary>Directory for log files.</summary>
    string LogsDirectory { get; }

    /// <summary>Full path of the settings.json file.</summary>
    string SettingsFilePath { get; }
}
