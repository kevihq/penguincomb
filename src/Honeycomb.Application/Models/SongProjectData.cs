using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Honeycomb.Application.Models;

/// <summary>
/// Serializable song-compilation project state. This is a faithful port of the legacy
/// WinForms <c>CompileSong.SaveData</c> class so existing <c>.ghproj</c> files keep
/// working (same field names, same <c>[DefaultValue]</c> attributes, same JSON behavior).
/// Implements <see cref="INotifyPropertyChanged"/> so the UI reflects changes made by
/// the compile pipeline (checksum renaming, author fill-in, path conversion, ...).
/// </summary>
public class SongProjectData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public int GhprojVersion = 1;

    private string _gameSelect = "";
    [DefaultValue("")]
    public string gameSelect { get => _gameSelect; set => SetProperty(ref _gameSelect, value); }

    private string _platformSelect = "";
    [DefaultValue("")]
    public string platformSelect { get => _platformSelect; set => SetProperty(ref _platformSelect, value); }

    private string _songName = "";
    [DefaultValue("")]
    public string songName { get => _songName; set => SetProperty(ref _songName, value); }

    private string _chartAuthor = "";
    [DefaultValue("")]
    public string chartAuthor { get => _chartAuthor; set => SetProperty(ref _chartAuthor, value); }

    private string _title = "";
    [DefaultValue("")]
    public string title { get => _title; set => SetProperty(ref _title, value); }

    private string _artist = "";
    [DefaultValue("")]
    public string artist { get => _artist; set => SetProperty(ref _artist, value); }

    private string _artistTextCustom = "";
    [DefaultValue("")]
    public string artistTextCustom { get => _artistTextCustom; set => SetProperty(ref _artistTextCustom, value); }

    private string _coverArtist = "";
    [DefaultValue("")]
    public string coverArtist { get => _coverArtist; set => SetProperty(ref _coverArtist, value); }

    private string _album = "";
    [DefaultValue("")]
    public string album { get => _album; set => SetProperty(ref _album, value); }

    private string _kickPath = "";
    [DefaultValue("")]
    public string kickPath { get => _kickPath; set => SetProperty(ref _kickPath, value); }

    private string _snarePath = "";
    [DefaultValue("")]
    public string snarePath { get => _snarePath; set => SetProperty(ref _snarePath, value); }

    private string _cymbalsPath = "";
    [DefaultValue("")]
    public string cymbalsPath { get => _cymbalsPath; set => SetProperty(ref _cymbalsPath, value); }

    private string _tomsPath = "";
    [DefaultValue("")]
    public string tomsPath { get => _tomsPath; set => SetProperty(ref _tomsPath, value); }

    private string _guitarPath = "";
    [DefaultValue("")]
    public string guitarPath { get => _guitarPath; set => SetProperty(ref _guitarPath, value); }

    private string _bassPath = "";
    [DefaultValue("")]
    public string bassPath { get => _bassPath; set => SetProperty(ref _bassPath, value); }

    private string _vocalsPath = "";
    [DefaultValue("")]
    public string vocalsPath { get => _vocalsPath; set => SetProperty(ref _vocalsPath, value); }

    private string _backingPaths = "";
    [DefaultValue("")]
    public string backingPaths { get => _backingPaths; set => SetProperty(ref _backingPaths, value); }

    private string _crowdPath = "";
    [DefaultValue("")]
    public string crowdPath { get => _crowdPath; set => SetProperty(ref _crowdPath, value); }

    private string _previewAudioPath = "";
    [DefaultValue("")]
    public string previewAudioPath { get => _previewAudioPath; set => SetProperty(ref _previewAudioPath, value); }

    private string _guitarPathGh3 = "";
    [DefaultValue("")]
    public string guitarPathGh3 { get => _guitarPathGh3; set => SetProperty(ref _guitarPathGh3, value); }

    private string _rhythmPathGh3 = "";
    [DefaultValue("")]
    public string rhythmPathGh3 { get => _rhythmPathGh3; set => SetProperty(ref _rhythmPathGh3, value); }

    private string _backingPathsGh3 = "";
    [DefaultValue("")]
    public string backingPathsGh3 { get => _backingPathsGh3; set => SetProperty(ref _backingPathsGh3, value); }

    private string _coopGuitarPath = "";
    [DefaultValue("")]
    public string coopGuitarPath { get => _coopGuitarPath; set => SetProperty(ref _coopGuitarPath, value); }

    private string _coopRhythmPath = "";
    [DefaultValue("")]
    public string coopRhythmPath { get => _coopRhythmPath; set => SetProperty(ref _coopRhythmPath, value); }

    private string _coopBackingPaths = "";
    [DefaultValue("")]
    public string coopBackingPaths { get => _coopBackingPaths; set => SetProperty(ref _coopBackingPaths, value); }

    private string _crowdPathGh3 = "";
    [DefaultValue("")]
    public string crowdPathGh3 { get => _crowdPathGh3; set => SetProperty(ref _crowdPathGh3, value); }

    private string _previewAudioPathGh3 = "";
    [DefaultValue("")]
    public string previewAudioPathGh3 { get => _previewAudioPathGh3; set => SetProperty(ref _previewAudioPathGh3, value); }

    private string _midiPathGh3 = "";
    [DefaultValue("")]
    public string midiPathGh3 { get => _midiPathGh3; set => SetProperty(ref _midiPathGh3, value); }

    private string _perfPathGh3 = "";
    [DefaultValue("")]
    public string perfPathGh3 { get => _perfPathGh3; set => SetProperty(ref _perfPathGh3, value); }

    private string _skaPathGh3 = "";
    [DefaultValue("")]
    public string skaPathGh3 { get => _skaPathGh3; set => SetProperty(ref _skaPathGh3, value); }

    private string _songScriptPathGh3 = "";
    [DefaultValue("")]
    public string songScriptPathGh3 { get => _songScriptPathGh3; set => SetProperty(ref _songScriptPathGh3, value); }

    private string _midiPath = "";
    [DefaultValue("")]
    public string midiPath { get => _midiPath; set => SetProperty(ref _midiPath, value); }

    private string _perfPath = "";
    [DefaultValue("")]
    public string perfPath { get => _perfPath; set => SetProperty(ref _perfPath, value); }

    private string _skaPath = "";
    [DefaultValue("")]
    public string skaPath { get => _skaPath; set => SetProperty(ref _skaPath, value); }

    private string _lipsyncPath = "";
    [DefaultValue("")]
    public string lipsyncPath { get => _lipsyncPath; set => SetProperty(ref _lipsyncPath, value); }

    private string _songScriptPath = "";
    [DefaultValue("")]
    public string songScriptPath { get => _songScriptPath; set => SetProperty(ref _songScriptPath, value); }

    private string _compilePath = "";
    [DefaultValue("")]
    public string compilePath { get => _compilePath; set => SetProperty(ref _compilePath, value); }

    private string _projectPath = "";
    [DefaultValue("")]
    public string projectPath { get => _projectPath; set => SetProperty(ref _projectPath, value); }

    private string _gameIcon = "";
    [DefaultValue("")]
    public string gameIcon { get => _gameIcon; set => SetProperty(ref _gameIcon, value); }

    private string _gameCategory = "";
    [DefaultValue("")]
    public string gameCategory { get => _gameCategory; set => SetProperty(ref _gameCategory, value); }

    private string _ghprojFromLoad = "";
    [DefaultValue("")]
    public string ghprojFromLoad { get => _ghprojFromLoad; set => SetProperty(ref _ghprojFromLoad, value); }

    private string _bandWtde = "";
    [DefaultValue("")]
    public string bandWtde { get => _bandWtde; set => SetProperty(ref _bandWtde, value); }

    private string _modsSubfolder = "";
    [DefaultValue("")]
    public string modsSubfolder { get => _modsSubfolder; set => SetProperty(ref _modsSubfolder, value); }

    private string _gSkeleton = "Default";
    [DefaultValue("Default")]
    public string gSkeleton { get => _gSkeleton; set => SetProperty(ref _gSkeleton, value); }

    private string _bSkeleton = "Default";
    [DefaultValue("Default")]
    public string bSkeleton { get => _bSkeleton; set => SetProperty(ref _bSkeleton, value); }

    private string _dSkeleton = "Default";
    [DefaultValue("Default")]
    public string dSkeleton { get => _dSkeleton; set => SetProperty(ref _dSkeleton, value); }

    private string _vSkeleton = "Default";
    [DefaultValue("Default")]
    public string vSkeleton { get => _vSkeleton; set => SetProperty(ref _vSkeleton, value); }

    private string _ghwtDrumkit = "Modern Rock";
    [DefaultValue("Modern Rock")]
    public string ghwtDrumkit { get => _ghwtDrumkit; set => SetProperty(ref _ghwtDrumkit, value); }

    private string _gh5Drumkit = "Modern Rock";
    [DefaultValue("Modern Rock")]
    public string gh5Drumkit { get => _gh5Drumkit; set => SetProperty(ref _gh5Drumkit, value); }

    private string _ghworDrumkit = "Modern Rock";
    [DefaultValue("Modern Rock")]
    public string ghworDrumkit { get => _ghworDrumkit; set => SetProperty(ref _ghworDrumkit, value); }

    private int _artistText;
    [DefaultValue(-1)]
    public int artistText { get => _artistText; set => SetProperty(ref _artistText, value); }

    private int _songYear = 2024;
    public int songYear { get => _songYear; set => SetProperty(ref _songYear, value); }

    private int _coverYear = 2024;
    public int coverYear { get => _coverYear; set => SetProperty(ref _coverYear, value); }

    private int _wtGenre;
    [DefaultValue(-1)]
    public int wtGenre { get => _wtGenre; set => SetProperty(ref _wtGenre, value); }

    private int _gh5Genre;
    [DefaultValue(-1)]
    public int gh5Genre { get => _gh5Genre; set => SetProperty(ref _gh5Genre, value); }

    private int _worGenre;
    [DefaultValue(-1)]
    public int worGenre { get => _worGenre; set => SetProperty(ref _worGenre, value); }

    private int _previewStart = 30000;
    [DefaultValue(30000)]
    public int previewStart { get => _previewStart; set => SetProperty(ref _previewStart, value); }

    private int _previewEnd = 30000;
    [DefaultValue(30000)]
    public int previewEnd { get => _previewEnd; set => SetProperty(ref _previewEnd, value); }

    private int _hmxHopoVal = 170;
    [DefaultValue(170)]
    public int hmxHopoVal { get => _hmxHopoVal; set => SetProperty(ref _hmxHopoVal, value); }

    private int _skaSourceGh3;
    public int skaSourceGh3 { get => _skaSourceGh3; set => SetProperty(ref _skaSourceGh3, value); }

    private int _venueSourceGh3;
    public int venueSourceGh3 { get => _venueSourceGh3; set => SetProperty(ref _venueSourceGh3, value); }

    private int _countoffGh3;
    public int countoffGh3 { get => _countoffGh3; set => SetProperty(ref _countoffGh3, value); }

    private int _vocalGenderGh3;
    public int vocalGenderGh3 { get => _vocalGenderGh3; set => SetProperty(ref _vocalGenderGh3, value); }

    private int _bassistSelect;
    public int bassistSelect { get => _bassistSelect; set => SetProperty(ref _bassistSelect, value); }

    private int _skaSource;
    public int skaSource { get => _skaSource; set => SetProperty(ref _skaSource, value); }

    private int _venueSource;
    public int venueSource { get => _venueSource; set => SetProperty(ref _venueSource, value); }

    private int _countoff;
    public int countoff { get => _countoff; set => SetProperty(ref _countoff, value); }

    private int _vocalGender;
    public int vocalGender { get => _vocalGender; set => SetProperty(ref _vocalGender, value); }

    private int _hopoMode;
    public int hopoMode { get => _hopoMode; set => SetProperty(ref _hopoMode, value); }

    private int _beat8thLow = 1;
    [DefaultValue(1)]
    public int beat8thLow { get => _beat8thLow; set => SetProperty(ref _beat8thLow, value); }

    private int _beat8thHigh = 150;
    [DefaultValue(150)]
    public int beat8thHigh { get => _beat8thHigh; set => SetProperty(ref _beat8thHigh, value); }

    private int _beat16thLow = 1;
    [DefaultValue(1)]
    public int beat16thLow { get => _beat16thLow; set => SetProperty(ref _beat16thLow, value); }

    private int _beat16thHigh = 120;
    [DefaultValue(120)]
    public int beat16thHigh { get => _beat16thHigh; set => SetProperty(ref _beat16thHigh, value); }

    private int _bandTier = 1;
    [DefaultValue(1)]
    public int bandTier { get => _bandTier; set => SetProperty(ref _bandTier, value); }

    private int _guitarTier = 1;
    [DefaultValue(1)]
    public int guitarTier { get => _guitarTier; set => SetProperty(ref _guitarTier, value); }

    private int _bassTier = 1;
    [DefaultValue(1)]
    public int bassTier { get => _bassTier; set => SetProperty(ref _bassTier, value); }

    private int _drumsTier = 1;
    [DefaultValue(1)]
    public int drumsTier { get => _drumsTier; set => SetProperty(ref _drumsTier, value); }

    private int _vocalsTier = 1;
    [DefaultValue(1)]
    public int vocalsTier { get => _vocalsTier; set => SetProperty(ref _vocalsTier, value); }

    private int _guitarCareerTier;
    [DefaultValue(0)]
    public int guitarCareerTier { get => _guitarCareerTier; set => SetProperty(ref _guitarCareerTier, value); }

    private int _bassCareerTier;
    [DefaultValue(0)]
    public int bassCareerTier { get => _bassCareerTier; set => SetProperty(ref _bassCareerTier, value); }

    private int _drumsCareerTier;
    [DefaultValue(0)]
    public int drumsCareerTier { get => _drumsCareerTier; set => SetProperty(ref _drumsCareerTier, value); }

    private int _vocalsCareerTier;
    [DefaultValue(0)]
    public int vocalsCareerTier { get => _vocalsCareerTier; set => SetProperty(ref _vocalsCareerTier, value); }

    private int _bandCareerTier;
    [DefaultValue(0)]
    public int bandCareerTier { get => _bandCareerTier; set => SetProperty(ref _bandCareerTier, value); }

    private decimal _gtrVolumeGh3;
    public decimal gtrVolumeGh3 { get => _gtrVolumeGh3; set => SetProperty(ref _gtrVolumeGh3, value); }

    private decimal _bandVolumeGh3;
    public decimal bandVolumeGh3 { get => _bandVolumeGh3; set => SetProperty(ref _bandVolumeGh3, value); }

    private decimal _vocalScrollSpeed = 1;
    [DefaultValue(1)]
    public decimal vocalScrollSpeed { get => _vocalScrollSpeed; set => SetProperty(ref _vocalScrollSpeed, value); }

    private decimal _vocalTuningCents;
    public decimal vocalTuningCents { get => _vocalTuningCents; set => SetProperty(ref _vocalTuningCents, value); }

    private decimal _sustainThreshold = 0.5m;
    [DefaultValue(0.5)]
    public decimal sustainThreshold { get => _sustainThreshold; set => SetProperty(ref _sustainThreshold, value); }

    private decimal _overallVolume;
    public decimal overallVolume { get => _overallVolume; set => SetProperty(ref _overallVolume, value); }

    private decimal _previewVolume = -7m;
    [DefaultValue(-7.0)]
    public decimal previewVolume { get => _previewVolume; set => SetProperty(ref _previewVolume, value); }

    private decimal _previewVolumeGh3 = -7m;
    [DefaultValue(-7.0)]
    public decimal previewVolumeGh3 { get => _previewVolumeGh3; set => SetProperty(ref _previewVolumeGh3, value); }

    private bool _isCover;
    public bool isCover { get => _isCover; set => SetProperty(ref _isCover, value); }

    private bool _isP2Rhythm;
    public bool isP2Rhythm { get => _isP2Rhythm; set => SetProperty(ref _isP2Rhythm, value); }

    private bool _isCoopAudio;
    public bool isCoopAudio { get => _isCoopAudio; set => SetProperty(ref _isCoopAudio, value); }

    private bool _useRenderedPreview;
    public bool useRenderedPreview { get => _useRenderedPreview; set => SetProperty(ref _useRenderedPreview, value); }

    private bool _useRenderedPreviewGh3;
    public bool useRenderedPreviewGh3 { get => _useRenderedPreviewGh3; set => SetProperty(ref _useRenderedPreviewGh3, value); }

    private bool _setEnd;
    public bool setEnd { get => _setEnd; set => SetProperty(ref _setEnd, value); }

    private bool _useBeatTrack;
    public bool useBeatTrack { get => _useBeatTrack; set => SetProperty(ref _useBeatTrack, value); }

    private bool _guitarMic;
    public bool guitarMic { get => _guitarMic; set => SetProperty(ref _guitarMic, value); }

    private bool _bassMic;
    public bool bassMic { get => _bassMic; set => SetProperty(ref _bassMic, value); }

    private bool _useNewClips;
    public bool useNewClips { get => _useNewClips; set => SetProperty(ref _useNewClips, value); }

    private bool _modernStrobes;
    public bool modernStrobes { get => _modernStrobes; set => SetProperty(ref _modernStrobes, value); }

    private bool _easyOpen;
    public bool easyOpen { get => _easyOpen; set => SetProperty(ref _easyOpen, value); }

    // Serialization helpers - mirrors the legacy JSON behavior so .ghproj files
    // remain compatible (ignore defaults on save, populate defaults on load).

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore });
    }

    public static SongProjectData? FromJson(string json)
    {
        return JsonConvert.DeserializeObject<SongProjectData>(json,
            new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Populate,
                NullValueHandling = NullValueHandling.Ignore
            });
    }
}
