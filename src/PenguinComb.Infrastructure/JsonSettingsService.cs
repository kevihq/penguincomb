using System.Xml.Linq;
using PenguinComb.Application.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PenguinComb.Infrastructure;

/// <summary>
/// Versioned JSON settings store with atomic writes and malformed-file recovery.
/// Unknown fields are preserved across saves so a newer settings file never loses
/// data when an older build rewrites it. On Windows, a one-time migration from the
/// legacy .NET user.config is attempted.
/// </summary>
public class JsonSettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly IPlatformService _platform;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private AppSettings _settings = new();
    private JObject? _raw;

    public JsonSettingsService(string filePath, IPlatformService platform)
    {
        _filePath = filePath;
        _platform = platform;
    }

    public AppSettings Settings => _settings;

    public event EventHandler? SettingsChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _settings = new AppSettings();
            _raw = new JObject();
            await TryMigrateLegacySettingsAsync(cancellationToken);
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            _raw = JObject.Parse(json);
            _settings = _raw.ToObject<AppSettings>() ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings file is malformed; starting with defaults. {ex.Message}");
            try
            {
                File.Copy(_filePath, _filePath + ".bak", overwrite: true);
            }
            catch
            {
                // Backup failure is non-fatal
            }
            _settings = new AppSettings();
            _raw = new JObject();
        }

        if (_settings.Version < 1)
        {
            _settings.Version = 1;
        }

        await TryMigrateLegacySettingsAsync(cancellationToken);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Merge current known values into the preserved raw object so unknown
            // (future) fields survive a round-trip.
            var current = JObject.FromObject(_settings);
            _raw ??= new JObject();
            foreach (var property in current.Properties())
            {
                _raw[property.Name] = property.Value;
            }

            string json = _raw.ToString(Formatting.Indented);
            string temp = _filePath + ".tmp";
            await File.WriteAllTextAsync(temp, json, cancellationToken);
            File.Move(temp, _filePath, overwrite: true); // atomic rename on the same volume
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// One-time migration from the legacy WinForms settings store
    /// (%LOCALAPPDATA%\GH_Toolkit_GUI\...\user.config). Only runs on Windows.
    /// </summary>
    private async Task TryMigrateLegacySettingsAsync(CancellationToken cancellationToken)
    {
        if (_platform.IsWindows is false)
        {
            return;
        }
        if (_settings.LegacySettingsMigrated)
        {
            return;
        }
        if (!_settings.IsEmpty())
        {
            // Do not clobber existing (non-default) settings with legacy values.
            _settings.LegacySettingsMigrated = true;
            await SaveAsync(cancellationToken);
            return;
        }

        string? userConfig = FindLegacyUserConfig();
        if (userConfig is null)
        {
            _settings.LegacySettingsMigrated = true;
            await SaveAsync(cancellationToken);
            return;
        }

        try
        {
            var doc = XDocument.Load(userConfig);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in doc.Descendants("setting"))
            {
                string? name = setting.Attribute("name")?.Value;
                string? value = setting.Element("value")?.Value;
                if (name != null && value != null)
                {
                    values[name] = value;
                }
            }

            ApplyLegacyValues(values);

            // Mark migrated so we never overwrite the legacy file's data again.
            _settings.LegacySettingsMigrated = true;
            await SaveAsync(cancellationToken);
            Console.WriteLine($"Migrated legacy settings from {userConfig}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Legacy settings migration failed: {ex.Message}");
            _settings.LegacySettingsMigrated = true;
            await SaveAsync(cancellationToken);
        }
    }

    private void ApplyLegacyValues(Dictionary<string, string> values)
    {
        string? Get(string key) => values.TryGetValue(key, out var v) ? v : null;

        if (decimal.TryParse(Get("PreviewFadeIn"), out var fadeIn)) _settings.PreviewFadeIn = fadeIn;
        if (decimal.TryParse(Get("PreviewFadeOut"), out var fadeOut)) _settings.PreviewFadeOut = fadeOut;
        if (bool.TryParse(Get("ShowPostCompile"), out var showPost)) _settings.ShowPostCompile = showPost;
        if (bool.TryParse(Get("EncryptAudio"), out var encrypt)) _settings.EncryptAudio = encrypt;
        if (bool.TryParse(Get("OverrideBeatLines"), out var beatLines)) _settings.OverrideBeatLines = beatLines;
        if (bool.TryParse(Get("SongManagerDeleteSongs"), out var deleteSongs)) _settings.SongManagerDeleteSongs = deleteSongs;
        if (bool.TryParse(Get("RecompileQb"), out var recompileQb)) _settings.RecompileQb = recompileQb;
        if (bool.TryParse(Get("DlcName"), out var dlcName)) _settings.DlcName = dlcName;
        if (bool.TryParse(Get("CompileToFolder"), out var compileToFolder)) _settings.CompileToFolder = compileToFolder;
        if (bool.TryParse(Get("Gh3Plus"), out var gh3Plus)) _settings.Gh3Plus = gh3Plus;
        if (int.TryParse(Get("ChecksumWarning"), out var checksumWarning)) _settings.ChecksumWarning = checksumWarning;

        CopyString(Get("Gh3QbPak"), v => _settings.Gh3QbPak = v);
        CopyString(Get("Gh3QbPab"), v => _settings.Gh3QbPab = v);
        CopyString(Get("GhaQbPak"), v => _settings.GhaQbPak = v);
        CopyString(Get("GhaQbPab"), v => _settings.GhaQbPab = v);
        CopyString(Get("Gh3FolderPath"), v => _settings.Gh3FolderPath = v);
        CopyString(Get("GhaFolderPath"), v => _settings.GhaFolderPath = v);
        CopyString(Get("WtModsFolder"), v => _settings.WtModsFolder = v);
        CopyString(Get("PreferredConsole"), v => _settings.PreferredConsole = v);

        string? onyx = Get("OnyxCliPath");
        if (!string.IsNullOrEmpty(onyx))
        {
            _settings.OnyxCliPath = onyx;
        }
    }

    private static void CopyString(string? value, Action<string> assign)
    {
        if (!string.IsNullOrEmpty(value))
        {
            assign(value);
        }
    }

    private string? FindLegacyUserConfig()
    {
        try
        {
            string localAppData = _platform.GetEnvironmentVariable("LOCALAPPDATA", "");
            if (string.IsNullOrEmpty(localAppData))
            {
                return null;
            }

            string guiRoot = Path.Combine(localAppData, "GH_Toolkit_GUI");
            if (!Directory.Exists(guiRoot))
            {
                return null;
            }

            return Directory.EnumerateFiles(guiRoot, "user.config", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

internal static class AppSettingsExtensions
{
    /// <summary>True when every string field is empty, every numeric field is the default, and all flags are false.</summary>
    public static bool IsEmpty(this AppSettings settings)
    {
        return settings.PreviewFadeIn == 1
            && settings.PreviewFadeOut == 1
            && settings.Gh3QbPak.Length == 0
            && settings.Gh3QbPab.Length == 0
            && settings.GhaQbPak.Length == 0
            && settings.GhaQbPab.Length == 0
            && settings.Gh3FolderPath.Length == 0
            && settings.GhaFolderPath.Length == 0
            && settings.ShowPostCompile
            && settings.WtModsFolder.Length == 0
            && settings.EncryptAudio
            && settings.OverrideBeatLines
            && settings.OnyxCliPath.Length == 0
            && settings.SongManagerDeleteSongs
            && settings.PreferredConsole == "Xbox 360"
            && settings.RecompileQb
            && settings.DlcName
            && !settings.CompileToFolder
            && !settings.Gh3Plus
            && settings.ChecksumWarning == 0;
    }
}
