using GH_Toolkit_Core.Methods;
using GH_Toolkit_Core.Other;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.PS360;
using GH_Toolkit_Core.QB;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;

namespace Honeycomb.Application.Services;

/// <summary>A single song loaded from an SGH archive.</summary>
public sealed record SghSongEntry(string Name, string Title, string Artist, QBStruct.QBStructData Data)
{
    public string DisplayName => $"{Name} ({Title} - {Artist})";
}

/// <summary>Result of loading an SGH file.</summary>
public sealed record SghLoadResult(IReadOnlyList<SghSongEntry> Songs, IReadOnlyList<string> Duplicates, string Folder);

/// <summary>
/// Imports songs from SGH archives into the game (PC or console). Ported from the
/// legacy <c>ImportSGH</c> and <c>SongListManager.ImportSghFuncs</c> logic.
/// </summary>
public class SghImportService
{
    private readonly ISettingsService _settings;
    private readonly IUserNotificationService _notifications;
    private readonly PreCompileChecks _checks;
    private readonly ResourceLocator _resources;

    public const string SghFileFilter = "SGH Files (*.sgh)|*.sgh|Zip Files (*.zip)|*.zip|All files (*.*)|*.*";

    public SghImportService(
        ISettingsService settings,
        IUserNotificationService notifications,
        PreCompileChecks checks,
        ResourceLocator resources)
    {
        _settings = settings;
        _notifications = notifications;
        _checks = checks;
        _resources = resources;
    }

    public AppSettings Pref => _settings.Settings;

    /// <summary>Extracts and reads the song list of an SGH archive.</summary>
    public SghLoadResult LoadSGH(string sghPath)
    {
        var duplicates = new List<string>();
        var masterList = new Dictionary<string, QBStruct.QBStructData>();
        var entries = new List<SghSongEntry>();

        string sghFolder = Path.Combine(Path.GetDirectoryName(sghPath)!, Path.GetFileNameWithoutExtension(sghPath));
        Directory.CreateDirectory(sghFolder);
        try
        {
            GHTCP.ExtractSongsFromSgh(sghPath, sghFolder, out bool isEncrypted);
            var songs = Path.Combine(sghFolder, "songs.info");
            var songsQb = Path.Combine(sghFolder, "songs.qb");
            if (isEncrypted)
            {
                var sghSongData = File.ReadAllBytes(songs);
                var decryptedSong = GHTCP.DecryptSongs(sghSongData);
                File.WriteAllBytes(songs, decryptedSong);
            }

            File.Move(songs, songsQb, true);
            var songsQbList = QB.DecompileQbFromFile(songsQb);
            foreach (var song in songsQbList)
            {
                if (song.Data is not QBStruct.QBStructData songData)
                {
                    continue;
                }

                var songName = songData["name"] as string ?? "";
                var songTitle = songData["title"] as string ?? "";
                var songArtist = songData["artist"] as string ?? "";

                if (masterList.ContainsKey(songName))
                {
                    duplicates.Add(songName);
                    continue;
                }

                masterList[songName] = songData;
                entries.Add(new SghSongEntry(songName, songTitle, songArtist, songData));
            }

            return new SghLoadResult(entries, duplicates, sghFolder);
        }
        finally
        {
            if (Directory.Exists(sghFolder))
            {
                Directory.Delete(sghFolder, true);
            }
        }
    }

    /// <summary>
    /// Converts the selected songs into the game (PC) or a console package.
    /// </summary>
    /// <param name="sghPath">Path of the SGH archive.</param>
    /// <param name="selectedSongs">Display names of the checked songs (first token is the short name).</param>
    /// <param name="console">"PC", "360" or "PS3" (matches the legacy console selector).</param>
    public async Task ConvertSongsAsync(string sghPath, IReadOnlyList<string> selectedSongs, string console, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string compilePath = Path.Combine(Path.GetDirectoryName(sghPath)!, Path.GetFileNameWithoutExtension(sghPath));
        string game = GAME_GH3;

        try
        {
            // The extraction-heavy prologue (the archive is extracted twice: once for
            // the song list, once for the song files) runs off the UI thread. All
            // interactive prompts below stay on the caller's thread.
            var prep = await Task.Run(() => PrepareCompile(sghPath, progress), ct);
            var masterList = prep.MasterList;
            var sectionDict = prep.SectionDict;

            if (masterList.Count == 0)
            {
                await _notifications.ShowMessageAsync("No Songs Loaded", "No songs loaded!\n\nPlease import an SGH file first.", ct);
                return;
            }

            var toImport = new List<QBStruct.QBStructData>();

            var failedSongs = new List<string>();
            var badNames = ForbiddenChecksums.GetForbiddenChecksums(game);

            foreach (string song in selectedSongs)
            {
                string songName = song.Split(' ')[0];
                try
                {
                    if (badNames.Contains(songName))
                    {
                        throw new Exception($"Song short name '{songName}' is not allowed due to being part of the main game.");
                    }
                    if (!masterList.TryGetValue(songName, out var songData))
                    {
                        throw new Exception($"Song '{songName}' not found in the SGH file.");
                    }
                    toImport.Add(songData);
                }
                catch
                {
                    failedSongs.Add(song);
                }
            }

            if (failedSongs.Count > 0)
            {
                int successCount = selectedSongs.Count - failedSongs.Count;
                await _notifications.ShowMessageAsync("Failed to Import Songs",
                    $"The following songs failed to import due to bad short names:\n\n{string.Join("\n", failedSongs)}\n\nPress OK to continue with the remaining {successCount} songs.", ct);
            }

            if (toImport.Count == 0)
            {
                throw new InvalidOperationException("No songs could be imported.");
            }

            PrepareFileNames(compilePath);

            var first = toImport[0];
            string[] checksumStrings = [(string)first["checksum"], (string)first["Title"], (string)first["Artist"], "123456"];
            var checksum = CreateForGame.MakeConsoleChecksum(checksumStrings);

            if (console == CONSOLE_PC)
            {
                await _checks.Gh3PcCheckAsync(game, cancellationToken: ct);
                var gameFolder = _checks.GetCustomsPak(game);
                var (pakData, pabData) = CreateForGame.AddToDownloadList(gameFolder, CONSOLE_PC, toImport);
                var musicFolder = Path.Combine(_checks.GetGh3Folder(game), "DATA", "MUSIC");
                var songsFolder = Path.Combine(_checks.GetGh3Folder(game), "DATA", "SONGS");

                foreach (var song in toImport)
                {
                    ct.ThrowIfCancellationRequested();
                    var songName = (string)song["name"];
                    var fsbPath = Path.Combine(compilePath, $"{songName}.fsb");
                    var datPath = Path.Combine(compilePath, $"{songName}.dat");
                    var songPath = Path.Combine(compilePath, $"{songName}_song.pak");
                    var fsbPathGame = Path.Combine(musicFolder, $"{songName}.fsb.xen");
                    var datPathGame = Path.Combine(musicFolder, $"{songName}.dat.xen");
                    var songPathGame = Path.Combine(songsFolder, $"{songName}_song.pak.xen");

                    if (!File.Exists(fsbPath)) throw new Exception($"Cannot find {songName} FSB in SGH file.");
                    if (!File.Exists(datPath)) throw new Exception($"Cannot find {songName} DAT in SGH file.");
                    if (!File.Exists(songPath)) throw new Exception($"Cannot find {songName} song PAK in SGH file.");

                    if (sectionDict != null)
                    {
                        RemapSections(songPath, songName, sectionDict, game);
                    }

                    File.Move(fsbPath, fsbPathGame, true);
                    File.Move(datPath, datPathGame, true);
                    File.Move(songPath, songPathGame, true);
                }

                _checks.OverwriteGh3Pak(pakData, pabData!, game);
            }
            else
            {
                await _checks.OnyxCheckAsync(ct);
                string sghName = $"{compilePath}_{console}";
                string sghFolderName = CreateForGame.ReplaceNonAlphanumeric(Path.GetFileName(compilePath));

                // Console packaging (per-song PAK compilation + Onyx invocation) is slow;
                // run it off the UI thread. OnyxCheckAsync above keeps the interactive
                // part on the UI thread.
                await Task.Run(() =>
                {
                    CreateConsoleDownloadFilesGh3(checksum, GAME_GH3, console, compilePath, _resources.ResourcesPath, toImport);
                    string[] onyxArgs;

                    if (console == CONSOLE_PS3)
                    {
                        string gameFiles = Path.Combine(compilePath, "USRDIR", sghFolderName.ToUpper());
                        Directory.CreateDirectory(gameFiles);
                        string ps3Resources = Path.Combine(_resources.ResourcesPath, "PS3");
                        string currGameResources = Path.Combine(ps3Resources, game);
                        string vramFile = Path.Combine(ps3Resources, $"VRAM_{game}");
                        if (!Directory.Exists(ps3Resources) || !Directory.Exists(currGameResources))
                        {
                            throw new Exception("Cannot find PS3 Resource folder.\n\nThis should be included with your toolkit.\nPlease re-download the toolkit.");
                        }

                        string contentID = FileCreation.GetPs3Key(GAME_GH3) + $"-{checksum.ToString().PadLeft(16, '0')}";
                        foreach (var file in Directory.GetFiles(compilePath))
                        {
                            File.Move(file, Path.Combine(gameFiles, $"{Path.GetFileName(file)}.PS3".ToUpper()), true);
                            string fileExtension = Path.GetExtension(file);
                            string fileNoExt = Path.GetFileNameWithoutExtension(file).ToLower();
                            bool localeFile = fileNoExt.Contains("_text") && !fileNoExt.EndsWith("_text");
                            if (fileExtension.ToLower() == ".pak" && !localeFile)
                            {
                                File.Copy(vramFile, Path.Combine(gameFiles, $"{fileNoExt}_VRAM.PAK.PS3".ToUpper()), true);
                            }
                        }
                        foreach (string file in Directory.GetFiles(currGameResources))
                        {
                            File.Copy(file, Path.Combine(compilePath, Path.GetFileName(file)), true);
                        }
                        onyxArgs = ["pkg", contentID, compilePath, "--to", sghName + ".PKG"];
                    }
                    else
                    {
                        onyxArgs = ["stfs", compilePath, "--to", sghName];
                        AddExtension(compilePath, ".xen");
                    }

                    Console.WriteLine("Creating package file...");
                    progress?.Report("Creating package file...");
                    CreateForGame.CompileWithOnyx(Pref.OnyxCliPath, onyxArgs);
                }, ct);
            }

            await _notifications.ShowMessageAsync("Conversion Complete",
                "Songs have been successfully converted and are ready for use!", ct);
        }
        finally
        {
            if (Directory.Exists(compilePath))
            {
                Directory.Delete(compilePath, true);
            }
        }
    }

    /// <summary>
    /// Extraction-heavy prologue of <see cref="ConvertSongsAsync"/>. Runs on a
    /// background thread and contains no user interaction.
    /// </summary>
    private CompilePrep PrepareCompile(string sghPath, IProgress<string>? progress)
    {
        var masterList = LoadSGH(sghPath).Songs.ToDictionary(s => s.Name, s => s.Data);
        string compilePath = Path.Combine(Path.GetDirectoryName(sghPath)!, Path.GetFileNameWithoutExtension(sghPath));

        Console.WriteLine("Extracting all songs from SGH file...");
        progress?.Report("Extracting all songs from SGH file...");
        Directory.CreateDirectory(compilePath);
        GHTCP.ExtractSghZip(sghPath, compilePath, out _);

        Dictionary<string, QBItem>? sectionDict = null;
        var sectionPath = Path.Combine(compilePath, "sections.q");
        if (File.Exists(sectionPath))
        {
            var (qbFile, _) = QB.ParseQFromFile(sectionPath);
            sectionDict = QB.QbEntryDict(qbFile);
        }

        DeleteTempFiles(compilePath);
        return new CompilePrep(masterList, sectionDict);
    }

    private sealed record CompilePrep(Dictionary<string, QBStruct.QBStructData> MasterList, Dictionary<string, QBItem>? SectionDict);

    /// <summary>Re-maps section names inside the song's mid.qb using sections.q (ported marker remap).</summary>
    internal static void RemapSections(string songPath, string songName, Dictionary<string, QBItem> sectionDict, string game)
    {
        var pakEntries = PAK.PakEntriesFromFilepath(songPath);
        var songMid = $"songs/{songName}.mid.qb";
        foreach (var entry in pakEntries)
        {
            if (entry.FullName != songMid)
            {
                continue;
            }

            var markers = $"{songName}_markers";
            var songQb = QB.QbEntryDictFromBytes(entry.EntryData, "big", songName);
            if (songQb.TryGetValue(markers, out var qbMarkers))
            {
                var markerArray = qbMarkers.Data as QBArray.QBArrayNode;
                if (markerArray == null)
                {
                    break;
                }
                foreach (QBStruct.QBStructData marker in markerArray.Items)
                {
                    var markerData = (string)marker["marker"];
                    if (sectionDict.TryGetValue(markerData, out var markerString))
                    {
                        string newSection = (string)markerString.Data;
                        if (newSection != null)
                        {
                            marker["marker"] = newSection;
                        }
                    }
                }
            }
            var songQbBytes = QB.CompileQbFromDict(songQb, songMid, game, "PC");
            entry.OverwriteData(songQbBytes);
            var pakCompiler = new PAK.PakCompiler(game, "PC", split: false);
            var (newSectData, _) = pakCompiler.CompilePakEntries(pakEntries);
            File.WriteAllBytes(songPath, newSectData);
            break;
        }
    }

    private static void DeleteTempFiles(string compilePath)
    {
        string songsInfo = Path.Combine(compilePath, "songs.info");
        string setlistInfo = Path.Combine(compilePath, "setlist.info");
        if (File.Exists(songsInfo)) File.Delete(songsInfo);
        if (File.Exists(setlistInfo)) File.Delete(setlistInfo);
    }

    private static void PrepareFileNames(string compilePath)
    {
        foreach (string file in Directory.GetFiles(compilePath))
        {
            if (Path.GetExtension(file) == ".xen")
            {
                File.Move(file, Path.ChangeExtension(file, null), true);
            }
        }
    }

    private static void AddExtension(string compilePath, string extension)
    {
        foreach (string file in Directory.GetFiles(compilePath))
        {
            if (Path.GetExtension(file).ToLower() != extension.ToLower())
            {
                File.Move(file, file + extension, true);
            }
        }
    }
}
