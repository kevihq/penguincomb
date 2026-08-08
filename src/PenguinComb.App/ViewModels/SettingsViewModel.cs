using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.App.ViewModels;

/// <summary>
/// Settings &amp; Information dialog. Mirrors the legacy ProgramSettings form fields.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IFileDialogService _dialogs;

    public SettingsViewModel(ISettingsService settings, IFileDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
    }

    public AppSettings Pref => _settings.Settings;

    public IReadOnlyList<string> PreferredConsoleOptions { get; } = ["Xbox 360", "PS3"];
    public IReadOnlyList<string> DlcNameOptions { get; } = ["dlc123456789", "Checksum"];
    public IReadOnlyList<string> ChecksumWarningOptions { get; } =
        ["Always Show Warning", "Always Modify Checksum", "Always Cancel Compilation"];

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // ---- Tab 1: Compile a Song ----
    [ObservableProperty]
    private bool _showPostCompile;

    [ObservableProperty]
    private bool _overrideBeatLines;

    [ObservableProperty]
    private decimal _previewFadeIn = 1;

    [ObservableProperty]
    private decimal _previewFadeOut = 1;

    [ObservableProperty]
    private bool _encryptAudio;

    [ObservableProperty]
    private string _wtModsFolder = "";

    [ObservableProperty]
    private string _gh3FolderPath = "";

    [ObservableProperty]
    private string _ghaFolderPath = "";

    [ObservableProperty]
    private string _onyxCliPath = "";

    [ObservableProperty]
    private string _ffmpegPath = "";

    [ObservableProperty]
    private int _preferredConsoleIndex;

    [ObservableProperty]
    private int _dlcShortnameIndex;

    [ObservableProperty]
    private bool _gh3Plus;

    [ObservableProperty]
    private int _checksumWarningIndex;

    // ---- Tab 2: Songlist Manager ----
    [ObservableProperty]
    private bool _songManagerDeleteSongs;

    /// <summary>Raised after Save &amp; Close so the window can close itself.</summary>
    public event EventHandler? CloseRequested;

    public void LoadFromSettings()
    {
        ShowPostCompile = Pref.ShowPostCompile;
        OverrideBeatLines = Pref.OverrideBeatLines;
        PreviewFadeIn = Pref.PreviewFadeIn;
        PreviewFadeOut = Pref.PreviewFadeOut;
        EncryptAudio = Pref.EncryptAudio;
        WtModsFolder = Pref.WtModsFolder;
        Gh3FolderPath = Pref.Gh3FolderPath;
        GhaFolderPath = Pref.GhaFolderPath;
        OnyxCliPath = Pref.OnyxCliPath;
        FfmpegPath = Pref.FfmpegPath;
        PreferredConsoleIndex = Math.Clamp(Array.IndexOf(PreferredConsoleOptions.ToArray(), Pref.PreferredConsole), 0, 1);
        DlcShortnameIndex = Pref.DlcName ? 0 : 1;
        Gh3Plus = Pref.Gh3Plus;
        ChecksumWarningIndex = Math.Clamp(Pref.ChecksumWarning, 0, 2);
        SongManagerDeleteSongs = Pref.SongManagerDeleteSongs;
        HasUnsavedChanges = false;
    }

    partial void OnShowPostCompileChanged(bool value) => MarkDirty();
    partial void OnOverrideBeatLinesChanged(bool value) => MarkDirty();
    partial void OnPreviewFadeInChanged(decimal value) => MarkDirty();
    partial void OnPreviewFadeOutChanged(decimal value) => MarkDirty();
    partial void OnEncryptAudioChanged(bool value) => MarkDirty();
    partial void OnWtModsFolderChanged(string value) => MarkDirty();
    partial void OnGh3FolderPathChanged(string value) => MarkDirty();
    partial void OnGhaFolderPathChanged(string value) => MarkDirty();
    partial void OnOnyxCliPathChanged(string value) => MarkDirty();
    partial void OnFfmpegPathChanged(string value) => MarkDirty();
    partial void OnPreferredConsoleIndexChanged(int value) => MarkDirty();
    partial void OnDlcShortnameIndexChanged(int value) => MarkDirty();
    partial void OnGh3PlusChanged(bool value) => MarkDirty();
    partial void OnChecksumWarningIndexChanged(int value) => MarkDirty();
    partial void OnSongManagerDeleteSongsChanged(bool value) => MarkDirty();

    private void MarkDirty() => HasUnsavedChanges = true;

    [RelayCommand]
    private async Task BrowseGh3FolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the GH3 game folder", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            Gh3FolderPath = folder;
        }
    }

    [RelayCommand]
    private async Task BrowseGhaFolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the Guitar Hero Aerosmith game folder", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            GhaFolderPath = folder;
        }
    }

    [RelayCommand]
    private async Task BrowseModsFolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the GHWT MODS folder", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            WtModsFolder = folder;
        }
    }

    [RelayCommand]
    private async Task BrowseOnyxAsync(CancellationToken cancellationToken)
    {
        string? file = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Select the Onyx CLI executable",
            Filters = [new FileFilter("Onyx executable", "onyx.exe;onyx"), new FileFilter("All files", "*.*")]
        }, cancellationToken);
        if (file is not null)
        {
            OnyxCliPath = file;
        }
    }

    [RelayCommand]
    private async Task BrowseFfmpegAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the folder containing ffmpeg and ffprobe", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            FfmpegPath = folder;
        }
    }

    /// <summary>Persists all fields. Returns false when the user cancels an unsaved close.</summary>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        Pref.ShowPostCompile = ShowPostCompile;
        Pref.OverrideBeatLines = OverrideBeatLines;
        Pref.PreviewFadeIn = PreviewFadeIn;
        Pref.PreviewFadeOut = PreviewFadeOut;
        Pref.EncryptAudio = EncryptAudio;
        Pref.WtModsFolder = WtModsFolder;
        Pref.Gh3FolderPath = Gh3FolderPath;
        Pref.GhaFolderPath = GhaFolderPath;
        Pref.OnyxCliPath = OnyxCliPath;
        Pref.FfmpegPath = FfmpegPath;
        Pref.PreferredConsole = PreferredConsoleOptions[PreferredConsoleIndex];
        Pref.DlcName = DlcShortnameIndex == 0;
        Pref.Gh3Plus = Gh3Plus;
        Pref.ChecksumWarning = ChecksumWarningIndex;
        Pref.SongManagerDeleteSongs = SongManagerDeleteSongs;
        await _settings.SaveAsync(cancellationToken);
        HasUnsavedChanges = false;
        return true;
    }

    [RelayCommand]
    private async Task SaveAndCloseAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(cancellationToken);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
