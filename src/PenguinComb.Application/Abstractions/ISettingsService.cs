namespace PenguinComb.Application.Abstractions;

/// <summary>
/// Persists and exposes the application settings as versioned JSON.
/// Implementations must use atomic writes and recover from missing/malformed files.
/// </summary>
public interface ISettingsService
{
    /// <summary>The current settings. Never null; defaults are used when nothing is stored yet.</summary>
    AppSettings Settings { get; }

    /// <summary>Loads settings from disk (with malformed-file recovery).</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the current settings to disk (atomic write).</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised after settings are loaded or saved.</summary>
    event EventHandler? SettingsChanged;
}

/// <summary>The full set of user preferences. Mirrors the legacy WinForms settings surface.</summary>
public class AppSettings
{
    /// <summary>Schema version for future migrations.</summary>
    public int Version { get; set; } = 1;

    // Preview fade values (seconds)
    public decimal PreviewFadeIn { get; set; } = 1;
    public decimal PreviewFadeOut { get; set; } = 1;

    // Game file locations (kept in sync with the game install locator)
    public string Gh3QbPak { get; set; } = "";
    public string Gh3QbPab { get; set; } = "";
    public string GhaQbPak { get; set; } = "";
    public string GhaQbPab { get; set; } = "";
    public string Gh3FolderPath { get; set; } = "";
    public string GhaFolderPath { get; set; } = "";

    // Compile options
    public bool ShowPostCompile { get; set; } = true;
    public string WtModsFolder { get; set; } = "";
    public bool EncryptAudio { get; set; } = true;
    public bool OverrideBeatLines { get; set; } = true;
    public bool CompileToFolder { get; set; } = false;
    public bool Gh3Plus { get; set; } = false;
    public int ChecksumWarning { get; set; } = 0; // 0 = warn, 1 = modify silently, 2 = cancel

    // Song list manager
    public bool SongManagerDeleteSongs { get; set; } = true;
    public bool RecompileQb { get; set; } = true;

    // Preferred console: "Xbox 360" or "PS3"
    public string PreferredConsole { get; set; } = "Xbox 360";
    public bool DlcName { get; set; } = true;

    // External tools. OnyxCliPath stores the full executable path (folder is accepted
    // for backwards compatibility with legacy settings).
    public string OnyxCliPath { get; set; } = "";

    /// <summary>Optional folder containing ffmpeg/ffprobe binaries. Empty = use PATH.</summary>
    public string FfmpegPath { get; set; } = "";

    /// <summary>True once the one-time Windows migration from legacy user.config has run.</summary>
    public bool LegacySettingsMigrated { get; set; } = false;
}
