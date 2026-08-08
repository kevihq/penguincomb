using GH_Toolkit_Core.Methods;
using GH_Toolkit_Core.QB;
using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Models;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.QB.QB;

namespace PenguinComb.Application.Models;

/// <summary>
/// Runtime state for a song-compilation session. Holds the serializable project data
/// plus transient fields that the legacy form kept as private members.
/// </summary>
public class SongProjectState
{
    public SongProjectData Data { get; set; } = new();

    public string CurrentGame { get; set; } = GameNames.GH3;
    public string CurrentPlatform { get; set; } = ConsoleNames.PC;

    public int PreviewStartTime { get; set; } = 30000;
    public int PreviewEndTime { get; set; } = 30000;

    public uint ConsoleChecksum { get; set; }
    public string ConsoleCompile { get; set; } = "";

    public Dictionary<string, int> WorldTourDiffs { get; set; } = new()
    {
        { "guitar", 1 },
        { "bass", 1 },
        { "drums", 1 },
        { "vocals", 1 }
    };

    public bool CompileExpertPlus { get; set; }
    public string EffectiveSongName { get; set; } = "";
    public string PakFilePath { get; set; } = "";

    /// <summary>NS hopo threshold for .q imports (legacy NumericUpDown default 2.95).</summary>
    public float NsHopoVal { get; set; } = 2.95f;

    /// <summary>Aerosmith band selection for GHA vocals (legacy ComboBox default).</summary>
    public string AerosmithBand { get; set; } = "aerosmith_band";
    public GhMetadata Metadata { get; set; } = new();
    public List<QBItem> SongList { get; set; } = new();
    public string[] QsStrings { get; set; } = [];
    public bool IsImport { get; set; }
    public bool RemakeAudio { get; set; } = true;

    // WTDE mod-folder layout (set during GHWT PC compiles)
    public string WtSongFolder { get; set; } = "";
    public string WtSongFolderExpertPlus { get; set; } = "";
    public string ContentFolder { get; set; } = "";
    public string ContentFolderExpertPlus { get; set; } = "";
    public string MusicFolder { get; set; } = "";
    public string MusicFolderExpertPlus { get; set; } = "";

    /// <summary>Path to the .ghproj file currently loaded (empty when unsaved).</summary>
    public string ProjectFilePath => Data.projectPath;
}

/// <summary>Options controlling a compilation run.</summary>
public sealed record CompileOptions
{
    public bool IsExport { get; init; }
    public bool IsAudioCompile { get; init; }
    public bool CompileToFolder { get; init; }
    /// <summary>When true the "compile finished" popup is shown on success.</summary>
    public bool ShowPostCompile { get; init; } = true;
}
