using GH_Toolkit_Core.Methods;
using GH_Toolkit_Core.Other;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.PAK.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBArray;
using static GH_Toolkit_Core.QB.QBConstants;
using static GH_Toolkit_Core.QB.QBStruct;

namespace Honeycomb.Application.Services;

/// <summary>
/// Song List Manager operations: load the customs setlist, delete songs, export an
/// SGH archive and restore the original setlist. Ported from the legacy
/// <c>SongListManager</c> form logic.
/// </summary>
public class SongListService
{
    private readonly ISettingsService _settings;
    private readonly IUserNotificationService _notifications;
    private readonly IFileDialogService _dialogs;
    private readonly PreCompileChecks _checks;
    private readonly SghImportService _sgh;

    public SongListService(
        ISettingsService settings,
        IUserNotificationService notifications,
        IFileDialogService dialogs,
        PreCompileChecks checks,
        SghImportService sgh)
    {
        _settings = settings;
        _notifications = notifications;
        _dialogs = dialogs;
        _checks = checks;
        _sgh = sgh;
    }

    public AppSettings Pref => _settings.Settings;

    /// <summary>Loads the customs setlist of the configured game into <paramref name="state"/>.</summary>
    public async Task<List<string>> LoadSetlistAsync(SongListState state, string game, CancellationToken ct = default)
    {
        await _checks.Gh3PcCheckAsync(game, cancellationToken: ct);

        state.Game = game;
        state.PakFile = _checks.GetCustomsPak(game);
        bool splitPak = game == GameNames.GH3 ? false : true;
        state.Compiler = new PakCompiler(GAME_GH3, CONSOLE_PC, split: splitPak);
        state.QbPak = PAK.PakEntryDictFromFile(state.PakFile);

        var (songlist, songListEntries, dlSongList, dlSongListProps) = GetSongListPak(state.QbPak);
        state.Songlist = songlist;
        state.SongListEntries = songListEntries;
        state.DlSongList = dlSongList;
        state.DlSongListProps = dlSongListProps;

        if (songListEntries.TryGetValue(gh3DownloadSongs, out QBItem dlSongs))
        {
            state.DownloadQb = null;
            state.DownloadQbEntries = null;
            state.DownloadList = dlSongs.Data as QBStruct.QBStructData;
        }
        else
        {
            (state.DownloadQb, state.DownloadQbEntries, state.DownloadList) = GetDownloadPak(state.QbPak);
        }

        state.Tier1 = state.DownloadList?["tier1"] as QBStructData;
        state.SongArray = state.Tier1?["songs"] as QBArrayNode;
        state.IsLoaded = state.SongArray != null;

        var songs = new List<string>();
        if (state.DlSongListProps != null && state.SongArray != null)
        {
            foreach (string songHash in state.SongArray.Items)
            {
                if (state.DlSongListProps[songHash] is not QBStruct.QBStructData song)
                {
                    continue;
                }
                var songName = song["name"] as string ?? "";
                var songTitle = song["title"] as string ?? "";
                var songArtist = song["artist"] as string ?? "";
                songs.Add($"{songName} ({songTitle} - {songArtist})");
            }
        }

        return songs;
    }

    /// <summary>Deletes the selected songs from the setlist (and optionally the audio/pak files).</summary>
    public async Task DeleteSongsAsync(SongListState state, IReadOnlyList<string> selectedSongs, CancellationToken ct = default)
    {
        if (selectedSongs.Count == 0)
        {
            await _notifications.ShowMessageAsync("No Songs Selected", "No songs selected to delete.", ct);
            return;
        }

        var musicFolder = Path.Combine(_checks.GetGh3Folder(state.Game), "DATA", "MUSIC");
        var songsFolder = Path.Combine(_checks.GetGh3Folder(state.Game), "DATA", "SONGS");
        var forbiddenSongs = ForbiddenChecksums.GetForbiddenChecksums(state.Game);

        var songArrayIndeces = new List<int>();
        var dlSonglistIndeces = new List<int>();
        var dlSongPropsIndeces = new List<int>();

        foreach (string song in selectedSongs)
        {
            ct.ThrowIfCancellationRequested();
            var songName = song.Split(' ')[0];
            songArrayIndeces.Add(state.SongArray!.GetItemIndex(songName, QBKEY));
            dlSonglistIndeces.Add(state.DlSongList!.GetItemIndex(songName, QBKEY));

            for (int i = 0; i < state.DlSongListProps!.Items.Count; i++)
            {
                var item = state.DlSongListProps.Items[i] as QBStruct.QBStructItem;
                var itemData = item!.Data as QBStruct.QBStructData;
                if (itemData?["name"] as string == songName)
                {
                    dlSongPropsIndeces.Add(i);
                    break;
                }
            }

            if (Pref.SongManagerDeleteSongs && !forbiddenSongs.Contains(songName))
            {
                var fsbPath = Path.Combine(musicFolder, $"{songName}.fsb.xen");
                var datPath = Path.Combine(musicFolder, $"{songName}.dat.xen");
                var songPath = Path.Combine(songsFolder, $"{songName}_song.pak.xen");
                if (File.Exists(fsbPath)) File.Delete(fsbPath);
                if (File.Exists(datPath)) File.Delete(datPath);
                if (File.Exists(songPath)) File.Delete(songPath);
            }
        }

        songArrayIndeces.Sort();
        dlSonglistIndeces.Sort();
        dlSongPropsIndeces.Sort();

        for (int i = songArrayIndeces.Count - 1; i >= 0; i--) state.SongArray!.Items.RemoveAt(songArrayIndeces[i]);
        for (int i = dlSonglistIndeces.Count - 1; i >= 0; i--) state.DlSongList!.Items.RemoveAt(dlSonglistIndeces[i]);
        for (int i = dlSongPropsIndeces.Count - 1; i >= 0; i--) state.DlSongListProps!.Items.RemoveAt(dlSongPropsIndeces[i]);

        var qbName = state.DownloadQb == null ? customsSonglistRef : songlistRef;
        byte[] songlistBytes = CompileQbFromDict(state.SongListEntries, qbName, GAME_GH3, CONSOLE_PC);
        state.Songlist!.OverwriteData(songlistBytes);

        if (state.DownloadQb != null && state.DownloadQbEntries != null)
        {
            byte[] dlSonglistBytes = CompileQbFromDict(state.DownloadQbEntries, downloadRef, GAME_GH3, CONSOLE_PC);
            state.DownloadQb.OverwriteData(dlSonglistBytes);
        }

        var (pakData, pabData) = state.Compiler!.CompilePakFromDictionary(state.QbPak);
        _checks.OverwriteGh3Pak(pakData, pabData!, state.Game);
    }

    /// <summary>Exports the selected songs as an SGH archive.</summary>
    public async Task ExportSongsAsync(SongListState state, IReadOnlyList<string> selectedSongs, CancellationToken ct = default)
    {
        string? sghPath = await _dialogs.PickSaveFileAsync(new FileDialogOptions
        {
            Title = "Export SGH",
            Filters = [new FileFilter("SGH Files", "*.sgh"), new FileFilter("Zip Files", "*.zip"), new FileFilter("All files", "*.*")],
            SuggestedFileName = "songs.sgh"
        }, ct);

        if (sghPath is null)
        {
            await _notifications.ShowMessageAsync("No File Selected", "No file selected to export SGH.", ct);
            return;
        }

        var sghFileName = Path.GetFileNameWithoutExtension(sghPath);
        var saveFolder = Path.GetDirectoryName(sghPath)!;
        var saveLocationForFiles = Path.Combine(saveFolder, sghFileName);
        Directory.CreateDirectory(saveLocationForFiles);

        var songsToExport = new List<QBItem>();
        var musicFolder = Path.Combine(_checks.GetGh3Folder(state.Game), "DATA", "MUSIC");
        var songsFolder = Path.Combine(_checks.GetGh3Folder(state.Game), "DATA", "SONGS");

        foreach (string song in selectedSongs)
        {
            ct.ThrowIfCancellationRequested();
            var songName = song.Split(' ')[0];
            Console.WriteLine($"Exporting song: {songName}");

            QBStruct.QBStructData? itemData = null;
            for (int i = 0; i < state.DlSongListProps!.Items.Count; i++)
            {
                var item = state.DlSongListProps.Items[i] as QBStruct.QBStructItem;
                itemData = item!.Data as QBStruct.QBStructData;
                if (itemData?["name"] as string == songName)
                {
                    break;
                }
            }

            if (itemData == null)
            {
                await _notifications.ShowErrorAsync("Error", $"Could not find song data for song: {songName}", ct);
                continue;
            }

            var fsbPath = Path.Combine(musicFolder, $"{songName}.fsb.xen");
            var datPath = Path.Combine(musicFolder, $"{songName}.dat.xen");
            var songPath = Path.Combine(songsFolder, $"{songName}_song.pak.xen");
            if (!(File.Exists(fsbPath) && File.Exists(datPath) && File.Exists(songPath)))
            {
                await _notifications.ShowWarningAsync("File Not Found", $"Missing files for song: {songName}", ct);
                continue;
            }

            songsToExport.Add(new QBItem(song, itemData));
            File.Copy(fsbPath, Path.Combine(saveLocationForFiles, $"{songName}.fsb.xen"), true);
            File.Copy(datPath, Path.Combine(saveLocationForFiles, $"{songName}.dat.xen"), true);
            File.Copy(songPath, Path.Combine(saveLocationForFiles, $"{songName}_song.pak.xen"), true);
        }

        var saveQb = Path.Combine(saveLocationForFiles, "songs.info");
        var bytes = CompileQbFile(songsToExport, "songs.info", GAME_GH3, CONSOLE_PC);
        File.WriteAllBytes(saveQb, bytes!);

        Console.WriteLine("Creating SGH...");
        if (File.Exists(sghPath))
        {
            File.Delete(sghPath);
        }
        GHTCP.MakeUnprotectedZip(saveLocationForFiles, sghPath);
        Directory.Delete(saveLocationForFiles, true);
        await _notifications.ShowMessageAsync("Export Complete", $"Exported SGH to: {sghPath}", ct);
    }

    /// <summary>Restores the original BetterGH3 setlist (removes all customs).</summary>
    public async Task RestoreSetlistAsync(string game, CancellationToken ct = default)
    {
        bool confirmed = await _notifications.ConfirmAsync("Confirm Restore",
            "Are you sure you want to restore the original DLC setlist? This will remove all custom songs from your setlist.", ct);
        if (!confirmed)
        {
            await _notifications.ShowMessageAsync("Cancelled", "Restore cancelled.", ct);
            return;
        }

        await _checks.Gh3PcCheckAsync(game, suppressMessages: true, cancellationToken: ct);
        await _notifications.ShowMessageAsync("Setlist Restored", "Original BetterGH3 setlist is now restored.", ct);
    }

    /// <summary>Loads an SGH file and returns its songs.</summary>
    public SghLoadResult LoadSgh(string sghPath) => _sgh.LoadSGH(sghPath);

    /// <summary>Imports selected SGH songs into the game.</summary>
    public Task ConvertSongsAsync(string sghPath, IReadOnlyList<string> selectedSongs, string console, IProgress<string>? progress = null, CancellationToken ct = default)
        => _sgh.ConvertSongsAsync(sghPath, selectedSongs, console, progress, ct);
}
