using PenguinComb.Application.Abstractions;

namespace PenguinComb.Infrastructure;

/// <summary>
/// Resolves per-user writable directories. Linux uses the XDG Base Directory
/// specification (with home-folder fallbacks); Windows uses %APPDATA%/%LOCALAPPDATA%.
/// All paths are created on first access.
/// </summary>
public class AppDataLocator : IAppDataLocator
{
    private readonly IPlatformService _platform;
    private readonly Lazy<string> _config;
    private readonly Lazy<string> _data;
    private readonly Lazy<string> _cache;

    public AppDataLocator(IPlatformService platform)
    {
        _platform = platform;

        // Test hook: when set, every directory derives from this root so tests never
        // touch the real user profile. Not set in normal operation.
        string? overrideRoot = Environment.GetEnvironmentVariable("PENGUINCOMB_OVERRIDE_DATA_ROOT");
        if (!string.IsNullOrEmpty(overrideRoot))
        {
            _config = new Lazy<string>(() => Path.Combine(overrideRoot, "config"));
            _data = new Lazy<string>(() => Path.Combine(overrideRoot, "data"));
            _cache = new Lazy<string>(() => Path.Combine(overrideRoot, "cache"));
            return;
        }

        _config = new Lazy<string>(ResolveConfigDirectory);
        _data = new Lazy<string>(ResolveDataDirectory);
        _cache = new Lazy<string>(ResolveCacheDirectory);
    }

    public string ConfigDirectory => _config.Value;
    public string DataDirectory => _data.Value;
    public string CacheDirectory => _cache.Value;
    public string BackupsDirectory => Path.Combine(DataDirectory, "Backups");
    public string TemplatesDirectory => Path.Combine(DataDirectory, "Templates");
    public string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    public string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");

    private static string CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private string ResolveConfigDirectory()
    {
        if (_platform.IsWindows)
        {
            string appData = _platform.GetEnvironmentVariable("APPDATA", "");
            return string.IsNullOrEmpty(appData) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PenguinComb") : Path.Combine(appData, "PenguinComb");
        }

        string xdgConfig = _platform.GetEnvironmentVariable("XDG_CONFIG_HOME", "");
        if (!string.IsNullOrEmpty(xdgConfig))
        {
            return Path.Combine(xdgConfig, "penguincomb");
        }
        string home = _platform.GetEnvironmentVariable("HOME", "");
        return Path.Combine(string.IsNullOrEmpty(home) ? "." : home, ".config", "penguincomb");
    }

    private string ResolveDataDirectory()
    {
        if (_platform.IsWindows)
        {
            string appData = _platform.GetEnvironmentVariable("APPDATA", "");
            return string.IsNullOrEmpty(appData) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PenguinComb") : Path.Combine(appData, "PenguinComb");
        }

        string xdgData = _platform.GetEnvironmentVariable("XDG_DATA_HOME", "");
        if (!string.IsNullOrEmpty(xdgData))
        {
            return Path.Combine(xdgData, "penguincomb");
        }
        string home = _platform.GetEnvironmentVariable("HOME", "");
        return Path.Combine(string.IsNullOrEmpty(home) ? "." : home, ".local", "share", "penguincomb");
    }

    private string ResolveCacheDirectory()
    {
        if (_platform.IsWindows)
        {
            string localAppData = _platform.GetEnvironmentVariable("LOCALAPPDATA", "");
            return string.IsNullOrEmpty(localAppData) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PenguinComb") : Path.Combine(localAppData, "PenguinComb");
        }

        string xdgCache = _platform.GetEnvironmentVariable("XDG_CACHE_HOME", "");
        if (!string.IsNullOrEmpty(xdgCache))
        {
            return Path.Combine(xdgCache, "penguincomb");
        }
        string home = _platform.GetEnvironmentVariable("HOME", "");
        return Path.Combine(string.IsNullOrEmpty(home) ? "." : home, ".cache", "penguincomb");
    }
}
