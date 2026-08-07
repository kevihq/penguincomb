using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using Honeycomb.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Honeycomb.App.ViewModels;

/// <summary>
/// View model for the song compiler window. Binds to <see cref="SongProjectData"/>
/// (which raises property-changed notifications) plus UI state that mirrors the
/// legacy WinForms form's dynamic behavior (tabs, platform gating, preview sync).
/// </summary>
public partial class CompileSongViewModel : ObservableObject
{
    private readonly SongCompileService _compile;
    private readonly ProjectFileService _projects;
    private readonly PreCompileChecks _checks;
    private readonly ISettingsService _settings;
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly ResourceLocator _resources;
    private readonly IServiceProvider _services;

    private bool _isProgrammaticChange;
    private bool _isLoading;
    private CancellationTokenSource? _cts;

    private readonly Dictionary<string, int> _gameGenres = new()
    {
        { "GHWT", 0 },
        { "GH5", 0 },
        { "GHWoR", 0 }
    };

    private readonly Dictionary<string, string> _gameDrumKits = new()
    {
        { "GHWT", "Modern Rock" },
        { "GH5", "Modern Rock" },
        { "GHWoR", "Modern Rock" }
    };

    private int _previewStartTime = 30000;
    private int _previewEndTime = 30000;

    public CompileSongViewModel(
        SongCompileService compile,
        ProjectFileService projects,
        PreCompileChecks checks,
        ISettingsService settings,
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        ResourceLocator resources,
        IServiceProvider services)
    {
        _compile = compile;
        _projects = projects;
        _checks = checks;
        _settings = settings;
        _dialogs = dialogs;
        _notifications = notifications;
        _resources = resources;
        _services = services;

        SkeletonOptions = LoadList(_resources.SkeletonsPath, ["Default"]);
        var categories = LoadList(_resources.SongCategoriesPath, []);
        GameCategoryOptions = categories;
        GameIconOptions = categories.Select(c => $"gamelogo_{c}").ToArray();

        // Defaults from the legacy OneTimeSetup
        SelectedGame = "GH3";
        SelectedPlatform = "PC";
        Data.songYear = DateTime.Now.Year;
        Data.artistText = 0;
        Data.skaSourceGh3 = 0;
        Data.venueSourceGh3 = 0;
        Data.countoffGh3 = 0;
        Data.vocalGenderGh3 = 0;
        Data.bassistSelect = 0;
        Data.hopoMode = 0;
        Data.skaSource = 0;
        Data.venueSource = 2;
        Data.countoff = 0;
        Data.vocalGender = 0;

        _settings.SettingsChanged += (_, _) =>
        {
            IsBeatLinesEnabled = _settings.Settings.OverrideBeatLines;
        };

        _projects.EnsureDefaultTemplate(Data);
        LoadProject(_projects.DefaultTemplatePath, isTemplate: true);
    }

    private static string[] LoadList(string path, string[] fallback)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path) : [];
        return lines.Length == 0 ? fallback : lines;
    }

    // =====================================================================
    // Model + state
    // =====================================================================

    public SongProjectState State { get; } = new();
    public SongProjectData Data => State.Data;

    public IReadOnlyList<string> ArtistTextOptions { get; } = SongCompileService.ArtistsText;
    public IReadOnlyList<string> CountoffOptions { get; } = SongCompileService.Countoffs;
    public IReadOnlyList<string> VocalGenderOptions { get; } = SongCompileService.VocalGenders;
    public IReadOnlyList<string> BassistOptions { get; } = SongCompileService.Bassists;
    public IReadOnlyList<string> VenueOptions { get; } = SongCompileService.Venues;
    public IReadOnlyList<string> SkaSourceOptions { get; } = SongCompileService.SkaSources;
    public IReadOnlyList<string> HopoModeOptions { get; } = SongCompileService.HopoModes;
    public IReadOnlyList<string> AerosmithBandOptions { get; } = SongCompileService.AerosmithBands;
    public IReadOnlyList<string> SkeletonOptions { get; }
    public IReadOnlyList<string> GameCategoryOptions { get; }
    public IReadOnlyList<string> GameIconOptions { get; }

    public ObservableCollection<string> GenreOptions { get; } = new();
    public ObservableCollection<string> DrumKitOptions { get; } = new();

    public ObservableCollection<string> BackingGh3 { get; } = new();
    public ObservableCollection<string> CoopBackingGh3 { get; } = new();
    public ObservableCollection<string> BackingWt { get; } = new();

    [ObservableProperty]
    private int _backingGh3Selection = -1;

    [ObservableProperty]
    private int _coopBackingGh3Selection = -1;

    [ObservableProperty]
    private int _backingWtSelection = -1;

    // =====================================================================
    // Game / platform selection
    // =====================================================================

    [ObservableProperty]
    private string _selectedGame = "GH3";

    partial void OnSelectedGameChanged(string value)
    {
        if (_isLoading)
        {
            return;
        }
        State.CurrentGame = value;
        Data.gameSelect = value;
        SetGameFields();
    }

    [ObservableProperty]
    private string _selectedPlatform = "PC";

    partial void OnSelectedPlatformChanged(string value)
    {
        if (_isLoading)
        {
            return;
        }
        State.CurrentPlatform = value;
        Data.platformSelect = value;
        DisplayChecksum();
        EnableCompileOnly();
    }

    [ObservableProperty]
    private int _selectedTabIndex;

    // Tab visibility (legacy SetTabs behavior)
    [ObservableProperty]
    private bool _isModernTabsVisible = true;

    [ObservableProperty]
    private bool _isGh3TabsVisible;

    [ObservableProperty]
    private bool _isWtdeSettingsVisible;

    [ObservableProperty]
    private bool _isGh5SettingsVisible;

    // Platform enablement (legacy EnablePlatforms behavior)
    [ObservableProperty]
    private bool _platformPcEnabled = true;

    [ObservableProperty]
    private bool _platformPs2Enabled;

    [ObservableProperty]
    private bool _platform360Enabled = true;

    [ObservableProperty]
    private bool _platformPs3Enabled = true;

    // Compile button state (legacy EnableCompileOnly behavior)
    [ObservableProperty]
    private string _compileButtonText = "Compile to Folder";

    [ObservableProperty]
    private bool _compileButtonEnabled = true;

    [ObservableProperty]
    private bool _isDlcChecksumVisible;

    [ObservableProperty]
    private string _dlcChecksumText = "";

    // Conditional field enablement
    [ObservableProperty]
    private bool _isBeatLinesEnabled = true;

    [ObservableProperty]
    private bool _isCoverFieldsEnabled;

    [ObservableProperty]
    private bool _isCoopAudioEnabled;

    [ObservableProperty]
    private bool _isCoopFieldsEnabled;

    [ObservableProperty]
    private bool _isArtistTextCustomEnabled;

    [ObservableProperty]
    private bool _isAerosmithBandEnabled;

    [ObservableProperty]
    private bool _isGh3PreviewFieldsEnabled = true;

    [ObservableProperty]
    private bool _isPreviewFieldsEnabled = true;

    [ObservableProperty]
    private bool _isGh3CrowdEnabled;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>NS HOPO threshold for .q MIDI imports (legacy NumericUpDown default 2.95).</summary>
    [ObservableProperty]
    private decimal _nsHopoValue = 2.95m;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private int _selectedGenreIndex;

    partial void OnSelectedGenreIndexChanged(int value)
    {
        if (_isProgrammaticChange || _isLoading || value < 0)
        {
            return;
        }
        _gameGenres[SelectedGame] = value;
        SaveGenreToData();
    }

    [ObservableProperty]
    private int _selectedDrumKitIndex;

    partial void OnSelectedDrumKitIndexChanged(int value)
    {
        if (_isProgrammaticChange || _isLoading || value < 0 || value >= DrumKitOptions.Count)
        {
            return;
        }
        _gameDrumKits[SelectedGame] = DrumKitOptions[value];
        SaveDrumKitToData();
    }

    private void SaveGenreToData()
    {
        switch (SelectedGame)
        {
            case "GHWT": Data.wtGenre = SelectedGenreIndex; break;
            case "GH5": Data.gh5Genre = SelectedGenreIndex; break;
            case "GHWoR": Data.worGenre = SelectedGenreIndex; break;
        }
    }

    private void SaveDrumKitToData()
    {
        switch (SelectedGame)
        {
            case "GHWT": Data.ghwtDrumkit = GetSelectedDrumKit(); break;
            case "GH5": Data.gh5Drumkit = GetSelectedDrumKit(); break;
            case "GHWoR": Data.ghworDrumkit = GetSelectedDrumKit(); break;
        }
    }

    private string GetSelectedDrumKit() =>
        SelectedDrumKitIndex >= 0 && SelectedDrumKitIndex < DrumKitOptions.Count
            ? DrumKitOptions[SelectedDrumKitIndex]
            : "Modern Rock";

    private void SetGameFields()
    {
        bool isOld = SelectedGame == "GH3" || SelectedGame == "GHA";
        EnablePlatforms();
        SetTabs(isOld);
        SetGenres();
        SetDrumKit();
        IsBeatLinesEnabled = _settings.Settings.OverrideBeatLines;
        IsGh3CrowdEnabled = SelectedGame == "GHA";
        OnVocalGenderChanged();
        SelectedTabIndex = 0;
        DisplayChecksum();
        EnableCompileOnly();
    }

    private void EnablePlatforms()
    {
        PlatformPcEnabled = true;
        PlatformPs2Enabled = false;
        Platform360Enabled = true;
        PlatformPs3Enabled = true;

        if (SelectedGame == "GH3" || SelectedGame == "GHA")
        {
            PlatformPs2Enabled = true;
        }
        else if (SelectedGame == "GHWT")
        {
            Platform360Enabled = false;
            PlatformPs3Enabled = false;
            if (SelectedPlatform != "PC")
            {
                SelectedPlatform = "PC";
                return;
            }
        }
        else
        {
            PlatformPcEnabled = false;
            if (SelectedPlatform == "PC" || SelectedPlatform == "PS2")
            {
                SelectedPlatform = _settings.Settings.PreferredConsole == "PS3" ? "PS3" : "Xbox 360";
                return;
            }
        }
    }

    private void SetTabs(bool isOld)
    {
        IsGh3TabsVisible = isOld;
        IsModernTabsVisible = !isOld;
        IsWtdeSettingsVisible = SelectedGame == "GHWT" && SelectedPlatform == "PC";
        IsGh5SettingsVisible = SelectedGame == "GH5" || SelectedGame == "GHWoR";
    }

    private void SetGenres()
    {
        var list = SelectedGame switch
        {
            "GHWT" => Genres.Wt,
            "GH5" => Genres.Gh5,
            "GHWoR" => Genres.Wor,
            _ => null
        };

        GenreOptions.Clear();
        if (list != null)
        {
            foreach (var genre in list)
            {
                GenreOptions.Add(genre);
            }
            int saved = _gameGenres.GetValueOrDefault(SelectedGame, 0);
            _isProgrammaticChange = true;
            SelectedGenreIndex = Math.Clamp(saved, 0, GenreOptions.Count - 1);
            _isProgrammaticChange = false;
        }
        SaveGenreToData();
    }

    private void SetDrumKit()
    {
        if (SelectedGame == "GH3" || SelectedGame == "GHA")
        {
            DrumKitOptions.Clear();
            SelectedDrumKitIndex = -1;
            return;
        }

        var baseKits = new List<string> { "Classic Rock", "Electro", "Fusion", "Heavy Rock", "Hip Hop", "Modern Rock" };
        if (SelectedGame == "GHWT")
        {
            baseKits.AddRange(["Blip Hop", "Cheesy", "Computight", "Conga", "Dub", "Eightys", "Gunshot", "House", "India", "Jazzy", "Old School", "Orchestral", "Scratch", "Scratch_Electro"]);
            if (SelectedPlatform == "PC")
            {
                baseKits.Add("And Justice For All");
            }
        }
        else
        {
            baseKits.AddRange(["Bigroom Rock", "Dance", "Metal", "Noise", "Standard Rock"]);
        }

        baseKits.Sort();
        DrumKitOptions.Clear();
        foreach (var kit in baseKits)
        {
            DrumKitOptions.Add(kit);
        }

        string current = _gameDrumKits.GetValueOrDefault(SelectedGame, "Modern Rock");
        int index = DrumKitOptions.IndexOf(current);
        _isProgrammaticChange = true;
        SelectedDrumKitIndex = index >= 0 ? index : DrumKitOptions.IndexOf("Modern Rock");
        _isProgrammaticChange = false;
        SaveDrumKitToData();
    }

    private void DisplayChecksum()
    {
        if (SelectedPlatform == "Xbox 360" || SelectedPlatform == "PS3")
        {
            _compile.SetConsoleChecksum(State);
            IsDlcChecksumVisible = true;
            DlcChecksumText = _settings.Settings.DlcName
                ? State.ConsoleChecksum.ToString()
                : Data.songName;
        }
        else
        {
            IsDlcChecksumVisible = false;
        }
    }

    private void EnableCompileOnly()
    {
        CompileButtonText = "Compile to Folder";
        CompileButtonEnabled = true;
        if (SelectedPlatform == "PC" && SelectedGame != "GHWT")
        {
            CompileButtonText = "Export to SGH";
        }
        else if (SelectedPlatform == "PS2" || SelectedGame == "GHWT")
        {
            CompileButtonText = "Disabled";
            CompileButtonEnabled = false;
        }
    }

    // =====================================================================
    // Cover / coop / rendered preview toggles
    // =====================================================================

    [ObservableProperty]
    private bool _isCover;

    partial void OnIsCoverChanged(bool value)
    {
        IsCoverFieldsEnabled = value;
        Data.isCover = value;
    }

    [ObservableProperty]
    private bool _isP2Rhythm;

    partial void OnIsP2RhythmChanged(bool value)
    {
        if (_isProgrammaticChange)
        {
            return;
        }
        IsCoopAudioEnabled = value;
        RhythmLabelText = value ? "Rhythm" : "Bass";
        if (!value)
        {
            Data.isCoopAudio = false;
        }
        Data.isP2Rhythm = value;
    }

    [ObservableProperty]
    private bool _isCoopAudio;

    partial void OnIsCoopAudioChanged(bool value)
    {
        if (_isProgrammaticChange)
        {
            return;
        }
        IsCoopFieldsEnabled = value;
        Data.isCoopAudio = value;
    }

    [ObservableProperty]
    private string _rhythmLabelText = "Bass";

    [ObservableProperty]
    private bool _useRenderedPreviewGh3;

    partial void OnUseRenderedPreviewGh3Changed(bool value)
    {
        if (_isProgrammaticChange)
        {
            return;
        }
        Data.useRenderedPreviewGh3 = value;
        IsGh3PreviewFieldsEnabled = !value;
    }

    [ObservableProperty]
    private bool _useRenderedPreview;

    partial void OnUseRenderedPreviewChanged(bool value)
    {
        if (_isProgrammaticChange)
        {
            return;
        }
        Data.useRenderedPreview = value;
        IsPreviewFieldsEnabled = !value;
    }

    [ObservableProperty]
    private bool _setEnd;

    partial void OnSetEndChanged(bool value)
    {
        if (_isProgrammaticChange)
        {
            return;
        }
        PreviewLengthEndSwap(value);
        Data.setEnd = value;
    }

    [ObservableProperty]
    private int _selectedArtistTextIndex;

    partial void OnSelectedArtistTextIndexChanged(int value)
    {
        if (_isProgrammaticChange || value < 0)
        {
            return;
        }
        Data.artistText = value;
        IsArtistTextCustomEnabled = value == 2; // "Other"
    }

    [ObservableProperty]
    private int _selectedVocalGenderGh3Index;

    partial void OnSelectedVocalGenderGh3IndexChanged(int value)
    {
        if (_isProgrammaticChange || value < 0)
        {
            return;
        }
        Data.vocalGenderGh3 = value;
        OnVocalGenderChanged();
    }

    private void OnVocalGenderChanged()
    {
        bool isSteven = Data.vocalGenderGh3 >= 0 && Data.vocalGenderGh3 < VocalGenderOptions.Count &&
                        VocalGenderOptions[Data.vocalGenderGh3] == "Steven Tyler";
        IsAerosmithBandEnabled = isSteven;
    }

    // =====================================================================
    // Preview time fields (12 sync + set-end semantics, ported from the form)
    // =====================================================================

    [ObservableProperty]
    private decimal _previewMinutesGh3;

    [ObservableProperty]
    private decimal _previewSecondsGh3;

    [ObservableProperty]
    private decimal _previewMillsGh3;

    [ObservableProperty]
    private decimal _lengthMinutesGh3;

    [ObservableProperty]
    private decimal _lengthSecondsGh3;

    [ObservableProperty]
    private decimal _lengthMillsGh3;

    [ObservableProperty]
    private decimal _previewMinutes;

    [ObservableProperty]
    private decimal _previewSeconds;

    [ObservableProperty]
    private decimal _previewMills;

    [ObservableProperty]
    private decimal _lengthMinutes;

    [ObservableProperty]
    private decimal _lengthSeconds;

    [ObservableProperty]
    private decimal _lengthMills;

    partial void OnPreviewMinutesGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewMinutes = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnPreviewSecondsGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewSeconds = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnPreviewMillsGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewMills = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnLengthMinutesGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthMinutes = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }
    partial void OnLengthSecondsGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthSeconds = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }
    partial void OnLengthMillsGh3Changed(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthMills = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }
    partial void OnPreviewMinutesChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewMinutesGh3 = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnPreviewSecondsChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewSecondsGh3 = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnPreviewMillsChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; PreviewMillsGh3 = value; UpdatePreviewStartTime(); _isProgrammaticChange = false; }
    partial void OnLengthMinutesChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthMinutesGh3 = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }
    partial void OnLengthSecondsChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthSecondsGh3 = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }
    partial void OnLengthMillsChanged(decimal value) { if (_isProgrammaticChange) return; _isProgrammaticChange = true; LengthMillsGh3 = value; UpdatePreviewEndTime(); _isProgrammaticChange = false; }

    private void UpdatePreviewStartTime()
    {
        _previewStartTime = ((int)PreviewMinutesGh3 * 60000) + ((int)PreviewSecondsGh3 * 1000) + (int)PreviewMillsGh3;
        Data.previewStart = _previewStartTime;
    }

    private void UpdatePreviewEndTime()
    {
        _previewEndTime = ((int)LengthMinutesGh3 * 60000) + ((int)LengthSecondsGh3 * 1000) + (int)LengthMillsGh3;
        Data.previewEnd = _previewEndTime;
    }

    private void PreviewLengthEndSwap(bool setEnd)
    {
        if (setEnd)
        {
            _previewEndTime = _previewStartTime + _previewEndTime;
        }
        else
        {
            _previewEndTime = _previewEndTime - _previewStartTime;
        }
        UpdatePreviewLengthFields();
    }

    private void UpdatePreviewLengthFields()
    {
        _isProgrammaticChange = true;
        LengthMinutesGh3 = _previewEndTime / 60000;
        LengthSecondsGh3 = (_previewEndTime % 60000) / 1000;
        LengthMillsGh3 = (_previewEndTime % 60000) % 1000;
        LengthMinutes = LengthMinutesGh3;
        LengthSeconds = LengthSecondsGh3;
        LengthMills = LengthMillsGh3;
        _isProgrammaticChange = false;
        Data.previewEnd = _previewEndTime;
    }

    private void UpdatePreviewFields()
    {
        if (_previewStartTime < 0) _previewStartTime = 0;
        if (_previewEndTime < 0) _previewEndTime = 0;

        _isProgrammaticChange = true;
        PreviewMinutesGh3 = _previewStartTime / 60000;
        PreviewSecondsGh3 = (_previewStartTime % 60000) / 1000;
        PreviewMillsGh3 = _previewStartTime % 1000;
        LengthMinutesGh3 = _previewEndTime / 60000;
        LengthSecondsGh3 = (_previewEndTime % 60000) / 1000;
        LengthMillsGh3 = _previewEndTime % 1000;
        PreviewMinutes = PreviewMinutesGh3;
        PreviewSeconds = PreviewSecondsGh3;
        PreviewMills = PreviewMillsGh3;
        LengthMinutes = LengthMinutesGh3;
        LengthSeconds = LengthSecondsGh3;
        LengthMills = LengthMillsGh3;
        _isProgrammaticChange = false;
    }

    // =====================================================================
    // Browse / list commands
    // =====================================================================

    private static readonly Dictionary<string, (string Kind, string Filter)> FieldKinds = new()
    {
        // GHWT audio (file pickers)
        ["kickPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["snarePath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["cymbalsPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["tomsPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["guitarPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["bassPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["vocalsPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["crowdPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["previewAudioPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        // GH3 audio
        ["guitarPathGh3"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["rhythmPathGh3"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["coopGuitarPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["coopRhythmPath"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["crowdPathGh3"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        ["previewAudioPathGh3"] = ("file", "Audio files (*.mp3, *.ogg, *.flac, *.wav)|*.mp3;*.ogg;*.flac;*.wav|All files (*.*)|*.*"),
        // GH3 song data
        ["midiPathGh3"] = ("file", "Guitar Hero Note Files (*.mid, *.chart, *.q)|*.mid;*.chart;*.q|MIDI files (*.mid)|*.mid|CHART files (*.chart)|*.chart|Q files (*.q)|*.q|All files (*.*)|*.*"),
        ["perfPathGh3"] = ("file", "Q files (*.q)|*.q|All files (*.*)|*.*"),
        ["skaPathGh3"] = ("folder", ""),
        ["songScriptPathGh3"] = ("file", "Q files (*.q)|*.q|All files (*.*)|*.*"),
        // GHWT song data
        ["midiPath"] = ("file", "Guitar Hero Note Files (*.mid, *.chart, *.q)|*.mid;*.chart;*.q|MIDI files (*.mid)|*.mid|CHART files (*.chart)|*.chart|Q files (*.q)|*.q|All files (*.*)|*.*"),
        ["perfPath"] = ("file", "Q files (*.q)|*.q|All files (*.*)|*.*"),
        ["skaPath"] = ("folder", ""),
        ["lipsyncPath"] = ("folder", ""),
        ["songScriptPath"] = ("file", "Q files (*.q)|*.q|All files (*.*)|*.*"),
        // General
        ["compilePath"] = ("folder", ""),
        ["projectPath"] = ("file", "GHProj files (*.ghproj)|*.ghproj|All files (*.*)|*.*"),
        ["modsSubfolder"] = ("folder", ""),
    };

    [RelayCommand]
    private async Task BrowseAsync(string field, CancellationToken cancellationToken)
    {
        if (!FieldKinds.TryGetValue(field, out var kind))
        {
            return;
        }

        if (kind.Kind == "file")
        {
            string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
            {
                Title = "Select file",
                Filters = ParseFilter(kind.Filter)
            }, cancellationToken);
            if (path is not null)
            {
                SetDataPath(field, path);
            }
        }
        else
        {
            string? folder = await _dialogs.PickFolderAsync("Select folder", cancellationToken: cancellationToken);
            if (folder is not null)
            {
                SetDataPath(field, folder);
            }
        }

        if (field == "modsSubfolder")
        {
            ValidateModsSubfolder();
        }
    }

    private void ValidateModsSubfolder()
    {
        try
        {
            string modsFolder = Path.GetFullPath(_settings.Settings.WtModsFolder);
            string inputPath;

            if (Path.IsPathRooted(Data.modsSubfolder))
            {
                inputPath = Path.GetFullPath(Data.modsSubfolder);
                if (!inputPath.StartsWith(modsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("The subfolder path must be within the MODS folder.");
                }
                Data.modsSubfolder = Path.GetRelativePath(modsFolder, inputPath);
            }
            else
            {
                inputPath = Path.GetFullPath(Path.Combine(modsFolder, Data.modsSubfolder));
                if (!inputPath.StartsWith(modsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Invalid relative MODS folder path. It escapes the MODS folder.");
                }
            }
        }
        catch (Exception ex)
        {
            Data.modsSubfolder = "";
            _notifications.ShowErrorAsync("Mods Subfolder Error", ex.Message).Wait();
        }
    }

    private void SetDataPath(string field, string value)
    {
        var prop = typeof(SongProjectData).GetProperty(field);
        if (prop != null)
        {
            prop.SetValue(Data, value);
        }
    }

    private static IReadOnlyList<FileFilter> ParseFilter(string filter)
    {
        var parts = filter.Split('|');
        var result = new List<FileFilter>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            result.Add(new FileFilter(parts[i], parts[i + 1]));
        }
        return result;
    }

    [RelayCommand]
    private async Task AddBackingAsync(string list, CancellationToken cancellationToken)
    {
        var files = await _dialogs.PickOpenFilesAsync(new FileDialogOptions
        {
            Title = "Select backing tracks",
            Filters = [new FileFilter("Audio files", "*.mp3;*.ogg;*.flac;*.wav"), new FileFilter("All files", "*.*")],
            AllowMultiple = true
        }, cancellationToken);

        var target = list switch
        {
            "gh3" => BackingGh3,
            "coop" => CoopBackingGh3,
            _ => BackingWt
        };
        foreach (var file in files)
        {
            target.Add(file);
        }
    }

    [RelayCommand]
    private void RemoveBacking(string list)
    {
        var (target, index) = list switch
        {
            "gh3" => (BackingGh3, BackingGh3Selection),
            "coop" => (CoopBackingGh3, CoopBackingGh3Selection),
            _ => (BackingWt, BackingWtSelection)
        };
        if (index >= 0 && index < target.Count)
        {
            target.RemoveAt(index);
        }
    }

    // =====================================================================
    // Project management
    // =====================================================================

    private void SyncListsFromData()
    {
        _isProgrammaticChange = true;
        BackingGh3.Clear();
        foreach (var p in Split(Data.backingPathsGh3)) BackingGh3.Add(p);
        CoopBackingGh3.Clear();
        foreach (var p in Split(Data.coopBackingPaths)) CoopBackingGh3.Add(p);
        BackingWt.Clear();
        foreach (var p in Split(Data.backingPaths)) BackingWt.Add(p);
        _isProgrammaticChange = false;
    }

    private void SyncDataFromLists()
    {
        Data.backingPathsGh3 = string.Join(";", BackingGh3);
        Data.coopBackingPaths = string.Join(";", CoopBackingGh3);
        Data.backingPaths = string.Join(";", BackingWt);
    }

    private static IEnumerable<string> Split(string? joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split(';', StringSplitOptions.RemoveEmptyEntries);

    public void LoadProject(string path, bool isTemplate = false)
    {
        try
        {
            var data = _projects.LoadProjectAsync(path).GetAwaiter().GetResult();
            if (data is null)
            {
                return;
            }

            _isLoading = true;
            ApplyData(data, isTemplate);
            _isLoading = false;
        }
        catch (Exception ex)
        {
            _notifications.ShowErrorAsync("Load Failed", $"Could not load the project file:\n\n{ex.Message}").Wait();
        }
    }

    private void ApplyData(SongProjectData data, bool isTemplate)
    {
        // Preserve per-game genre/drumkit maps
        _gameGenres["GHWT"] = data.wtGenre;
        _gameGenres["GH5"] = data.gh5Genre;
        _gameGenres["GHWoR"] = data.worGenre;
        _gameDrumKits["GHWT"] = string.IsNullOrEmpty(data.ghwtDrumkit) ? "Modern Rock" : data.ghwtDrumkit;
        _gameDrumKits["GH5"] = string.IsNullOrEmpty(data.gh5Drumkit) ? "Modern Rock" : data.gh5Drumkit;
        _gameDrumKits["GHWoR"] = string.IsNullOrEmpty(data.ghworDrumkit) ? "Modern Rock" : data.ghworDrumkit;

        State.Data = data;
        SyncListsFromData();

        // Game/platform radios (legacy LoadSaveData order: game first, then platform)
        string game = data.gameSelect == "GH3" || data.gameSelect == "GHA" || data.gameSelect == "GHWT" || data.gameSelect == "GH5" || data.gameSelect == "GHWoR" ? data.gameSelect : "GH3";
        SelectedGame = game;
        string platform = data.platformSelect is "PC" or "PS2" or "Xbox 360" or "PS3" ? data.platformSelect : "PC";
        SelectedPlatform = platform;

        _previewStartTime = data.previewStart;
        _previewEndTime = data.previewEnd;

        // Toggles (set programmatic flags around the dependent property setters)
        _isProgrammaticChange = true;
        IsCover = data.isCover;
        IsP2Rhythm = data.isP2Rhythm;
        IsCoopAudio = data.isCoopAudio;
        UseRenderedPreviewGh3 = data.useRenderedPreviewGh3;
        UseRenderedPreview = data.useRenderedPreview;
        SetEnd = data.setEnd;
        _isProgrammaticChange = false;

        // re-apply dependent states
        IsCoverFieldsEnabled = data.isCover;
        IsCoopAudioEnabled = data.isP2Rhythm;
        IsCoopFieldsEnabled = data.isCoopAudio;
        IsGh3PreviewFieldsEnabled = !data.useRenderedPreviewGh3;
        IsPreviewFieldsEnabled = !data.useRenderedPreview;

        // combo indexes
        _isProgrammaticChange = true;
        SelectedArtistTextIndex = data.artistText;
        SelectedVocalGenderGh3Index = data.vocalGenderGh3;
        _isProgrammaticChange = false;
        IsArtistTextCustomEnabled = data.artistText == 2;
        OnVocalGenderChanged();

        UpdatePreviewFields();
        SetGameFields();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        SyncDataFromLists();
        State.EffectiveSongName = Data.songName;
        await _projects.SaveProjectAsync(Data, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsAsync(CancellationToken cancellationToken)
    {
        SyncDataFromLists();
        await _projects.SaveProjectAsAsync(Data, cancellationToken);
    }

    [RelayCommand]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Open Project",
            Filters = [new FileFilter("GHProj files", "*.ghproj"), new FileFilter("All files", "*.*")]
        }, cancellationToken);
        if (path is not null)
        {
            LoadProject(path);
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        LoadProject(_projects.DefaultTemplatePath, isTemplate: true);
    }

    [RelayCommand]
    private async Task SaveTemplateAsync(CancellationToken cancellationToken)
    {
        SyncDataFromLists();
        var data = Data;
        data.projectPath = "";
        string json = data.ToJson();
        string? path = await _dialogs.PickSaveFileAsync(new FileDialogOptions
        {
            Title = "Save Template",
            Filters = [new FileFilter("GHProj files", "*.ghproj"), new FileFilter("All files", "*.*")],
            InitialDirectory = _projects.DefaultTemplateFolder,
            SuggestedFileName = "template.ghproj"
        }, cancellationToken);
        if (path is not null)
        {
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
    }

    [RelayCommand]
    private async Task LoadTemplateAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Load Template",
            Filters = [new FileFilter("GHProj files", "*.ghproj"), new FileFilter("All files", "*.*")],
            InitialDirectory = _projects.DefaultTemplateFolder
        }, cancellationToken);
        if (path is not null)
        {
            LoadProject(path, isTemplate: true);
            Data.projectPath = "";
        }
    }

    [RelayCommand]
    private async Task ImportChFolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the Clone Hero song folder you want to import", cancellationToken: cancellationToken);
        if (folder is null)
        {
            return;
        }
        try
        {
            _projects.EnsureDefaultTemplate(Data);
            _projects.LoadFromChFolder(Data, folder);
            _previewStartTime = Data.previewStart;
            _previewEndTime = Data.previewEnd;
            SyncListsFromData();
            UpdatePreviewFields();
        }
        catch (Exception ex)
        {
            await _notifications.ShowErrorAsync("Import Failed", ex.Message, cancellationToken);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new Views.SettingsWindow { DataContext = _services.GetRequiredService<SettingsViewModel>() };
        var vm = (SettingsViewModel)window.DataContext;
        vm.LoadFromSettings();
        window.ShowDialog(GetOwnerWindow());
        IsBeatLinesEnabled = _settings.Settings.OverrideBeatLines;
    }

    // =====================================================================
    // Compilation commands
    // =====================================================================

    [RelayCommand]
    private async Task CompileAllAsync(CancellationToken cancellationToken)
    {
        await RunCompileAsync(new CompileOptions { CompileToFolder = false, IsExport = false }, cancellationToken);
    }

    /// <summary>The primary "Compile" button: exports for PC/PS2, compiles to folder for consoles.</summary>
    [RelayCommand]
    private async Task CompilePaksAsync(CancellationToken cancellationToken)
    {
        if (SelectedPlatform == "PC" || SelectedPlatform == "PS2")
        {
            await RunCompileAsync(new CompileOptions { CompileToFolder = false, IsExport = true }, cancellationToken);
        }
        else
        {
            await RunCompileAsync(new CompileOptions { CompileToFolder = true, IsExport = false }, cancellationToken);
        }
    }

    [RelayCommand]
    private async Task CompileAudioAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            SyncDataFromLists();
            State.EffectiveSongName = Data.songName;
            State.NsHopoVal = (float)NsHopoValue;
            State.PreviewStartTime = _previewStartTime;
            State.PreviewEndTime = _previewEndTime;
            _projects.SetAllToAbsolute(Data);
            try
            {
                await _compile.CompileAudioOnlyAsync(State, encrypt: _settings.Settings.EncryptAudio && SelectedGame != "GHWT", new Progress<string>(p => StatusText = p), _cts.Token);
            }
            finally
            {
                _projects.SetAllToRelative(Data);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Compilation cancelled.";
        }
        catch (Exception ex)
        {
            await _notifications.ShowErrorAsync("Audio Compilation Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
            StatusText = "";
        }
    }

    [RelayCommand]
    private async Task ExportSongArchiveAsync(CancellationToken cancellationToken)
    {
        await RunCompileAsync(new CompileOptions { CompileToFolder = false, IsExport = true }, cancellationToken);
    }

    private async Task RunCompileAsync(CompileOptions options, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return; // prevent starting the same operation twice
        }

        IsBusy = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var result = new SongCompileResult { Success = false };
        try
        {
            SyncDataFromLists();
            State.EffectiveSongName = Data.songName;
            State.NsHopoVal = (float)NsHopoValue;
            State.PreviewStartTime = _previewStartTime;
            State.PreviewEndTime = _previewEndTime;
            _projects.SetAllToAbsolute(Data);
            try
            {
                result = await _compile.CompileAllAsync(State, options, new Progress<string>(p => StatusText = p), _cts.Token);
            }
            finally
            {
                _projects.SetAllToRelative(Data);
            }

            if (result.Success && _settings.Settings.ShowPostCompile && !options.IsAudioCompile)
            {
                ShowPostCompile(options);
            }
        }
        finally
        {
            IsBusy = false;
            StatusText = "";
        }
    }

    private void ShowPostCompile(CompileOptions options)
    {
        string title;
        string message;
        if (options.IsExport)
        {
            title = "Export Complete";
            message = "Export has completed successfully!\n\nYour song has been packaged up and is ready to be shared with others.\n\nIt can be found where you defined the song to be compiled to or next to your .ghproj file.";
        }
        else if (SelectedPlatform == "PC")
        {
            title = "Compilation Complete";
            message = "Compilation has completed successfully!\n\nYour song has been added to the game and can be played immediately.";
        }
        else
        {
            title = "Compilation Complete";
            message = "Compilation has completed successfully!\n\nYour song has been packaged up and is ready to be installed on your console.\n\nIt can be found where you defined the song to be compiled to or next to your .ghproj file.\n\nDon't forget to add it to your custom cache!";
        }

        _notifications.ShowMessageAsync(title, message).Wait();
    }

    private Window? GetOwnerWindow() =>
        App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    public void Dispose()
    {
        _cts?.Cancel();
    }
}
