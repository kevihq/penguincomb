using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using PenguinComb.Application.Abstractions;
using static GH_Toolkit_Core.PAK.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBArray;
using static GH_Toolkit_Core.QB.QBStruct;

namespace PenguinComb.Application.Models;

/// <summary>Runtime state for the Song List Manager (parsed customs PAK structure).</summary>
public class SongListState
{
    public string Game { get; set; } = "GH3";
    public string PakFile { get; set; } = "";
    public Dictionary<string, PakEntry> QbPak { get; set; } = new();
    public PakCompiler? Compiler { get; set; }
    public PakEntry? Songlist { get; set; }
    public Dictionary<string, QBItem> SongListEntries { get; set; } = new();
    public QBArrayNode? DlSongList { get; set; }
    public QBStructData? DlSongListProps { get; set; }
    public PakEntry? DownloadQb { get; set; }
    public Dictionary<string, QBItem>? DownloadQbEntries { get; set; }
    public QBStructData? DownloadList { get; set; }
    public QBStructData? Tier1 { get; set; }
    public QBArrayNode? SongArray { get; set; }
    public bool IsLoaded { get; set; }
}
