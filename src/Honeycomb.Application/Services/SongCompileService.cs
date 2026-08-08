using GH_Toolkit_Core.Audio;
using GH_Toolkit_Core.Checksum;
using GH_Toolkit_Core.Methods;
using GH_Toolkit_Core.Other;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.PS360;
using GH_Toolkit_Core.QB;
using GH_Toolkit_Core.SKA;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using static GH_Toolkit_Core.Methods.CreateForGame;
using static GH_Toolkit_Core.Methods.Exceptions;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;

namespace Honeycomb.Application.Services;

/// <summary>
/// Orchestrates the song-compilation pipeline (chart PAK, audio, console packages,
/// WTDE mods, SGH export). This is a faithful port of the legacy <c>CompileSong</c>
/// form logic with all UI access replaced by the injected services and the
/// <see cref="SongProjectData"/> model.
/// </summary>
public class SongCompileService
{
    private readonly ISettingsService _settings;
    private readonly IUserNotificationService _notifications;
    private readonly IExternalProcessService _processes;
    private readonly IPlatformService _platform;
    private readonly IFilePermissionService _permissions;
    private readonly PreCompileChecks _checks;
    private readonly ProjectFileService _projects;
    private readonly ResourceLocator _resources;

    private static readonly System.Globalization.CultureInfo Murica = new("en-US");

    public SongCompileService(
        ISettingsService settings,
        IUserNotificationService notifications,
        IExternalProcessService processes,
        IPlatformService platform,
        IFilePermissionService permissions,
        PreCompileChecks checks,
        ProjectFileService projects,
        ResourceLocator resources)
    {
        _settings = settings;
        _notifications = notifications;
        _processes = processes;
        _platform = platform;
        _permissions = permissions;
        _checks = checks;
        _projects = projects;
        _resources = resources;
    }

    public AppSettings Pref => _settings.Settings;

    // =====================================================================
    // Public entry points
    // =====================================================================

    /// <summary>Full compile: chart + audio + destination handling.</summary>
    public async Task<SongCompileResult> CompileAllAsync(
        SongProjectState state,
        CompileOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        bool suppressMessages = false)
    {
        string game = state.CurrentGame;
        Console.WriteLine($"Compiling chart and audio for {game}");
        progress?.Report($"Compiling chart and audio for {game}");

        state.CompileExpertPlus = false;
        bool compileSuccess = false;
        bool pakSuccess = false;
        DateTime time1 = DateTime.Now;

        try
        {
            if (game == GAME_GH3 || game == GAME_GHA)
            {
                pakSuccess = await CompilePakGh3Async(state, options, cancellationToken, suppressMessages);
                if (pakSuccess)
                {
                    await CompileGh3AudioAsync(state, options, cancellationToken, suppressMessages);
                    compileSuccess = true;
                }
            }
            else if (game == GAME_GHWT)
            {
                pakSuccess = await CompilePakGhwtAsync(state, options, cancellationToken, suppressMessages);
                if (pakSuccess)
                {
                    await CompileGhwtAudioAsync(state, options, encrypt: false, cancellationToken, suppressMessages);
                    compileSuccess = true;
                }
            }
            else
            {
                pakSuccess = await CompilePakGh5Async(state, options, cancellationToken, suppressMessages);
                if (pakSuccess)
                {
                    await CompileGhwtAudioAsync(state, options, encrypt: true, cancellationToken, suppressMessages);
                    compileSuccess = true;
                }
                MoveGh5Files(state);
                (state.SongList, state.QsStrings) = state.Metadata.GenerateGh5SongListEntry();
                if (!options.CompileToFolder)
                {
                    CreateConsoleDownloadFilesGh5(state.ConsoleChecksum, game, state.CurrentPlatform,
                        state.ConsoleCompile, _resources.ResourcesPath, state.SongList, state.QsStrings, state.Metadata.PackageName);
                }
                else
                {
                    string checksumOverride = GetSongChecksum(state, options);
                    CreateConsoleFolderGh5(checksumOverride, game, state.CurrentPlatform,
                        state.ConsoleCompile, _resources.ResourcesPath, state.SongList, state.QsStrings);
                }
            }

            if (!options.IsExport && compileSuccess && (state.CurrentPlatform == ConsoleNames.PS3 || state.CurrentPlatform == ConsoleNames.Xbox360))
            {
                CreateConsolePackage(state, options);
            }
            else if (compileSuccess && state.CurrentPlatform == ConsoleNames.PS2)
            {
                CreateConsoleFilesGh3Ps2(state);
            }
            else if (options.IsExport)
            {
                Console.WriteLine("Packing up the song for export...");
                GHTCP.MakeUnprotectedZip(state.ConsoleCompile, Path.Combine(state.Data.compilePath, $"{GetSongChecksum(state, options)}.sgh"));
            }

            TimeSpan timeDiff = DateTime.Now - time1;
            string process = compileSuccess ? (options.IsExport ? "Exporting took" : "Chart compilation took") : "Compilation failed after";
            Console.WriteLine($"{process} {timeDiff.TotalSeconds.ToString("G3", Murica)} seconds");

            return new SongCompileResult { Success = compileSuccess };
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Compilation cancelled.");
            return new SongCompileResult { Success = false, Cancelled = true };
        }
        catch (Exception ex)
        {
            compileSuccess = false;
            await HandleExceptionAsync(ex, "Compile Failed!", cancellationToken, showDialog: !suppressMessages);
            return new SongCompileResult { Success = false, Error = ex };
        }
    }

    /// <summary>Compile only the chart (no audio). Used by the "compile to folder" flow.</summary>
    public async Task<SongCompileResult> CompilePaksOnlyAsync(
        SongProjectState state,
        CompileOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        state.CompileExpertPlus = false;
        Console.WriteLine($"Compiling song for {state.CurrentGame}");
        progress?.Report($"Compiling song for {state.CurrentGame}");

        DateTime time1 = DateTime.Now;
        bool success;
        try
        {
            if (state.CurrentGame == GAME_GH3 || state.CurrentGame == GAME_GHA)
            {
                success = await CompilePakGh3Async(state, options, cancellationToken);
            }
            else if (state.CurrentGame == GAME_GHWT)
            {
                success = await CompilePakGhwtAsync(state, options, cancellationToken);
            }
            else
            {
                success = await CompilePakGh5Async(state, options, cancellationToken);
                if (success)
                {
                    MoveGh5Files(state);
                    (state.SongList, state.QsStrings) = state.Metadata.GenerateGh5SongListEntry();
                    CreateConsoleDownloadFilesGh5(state.ConsoleChecksum, state.CurrentGame, state.CurrentPlatform,
                        state.ConsoleCompile, _resources.ResourcesPath, state.SongList, state.QsStrings, state.Metadata.PackageName);
                }
            }

            if (success)
            {
                if (state.CurrentPlatform == ConsoleNames.PS3 || state.CurrentPlatform == ConsoleNames.Xbox360)
                {
                    CreateConsolePackage(state, options);
                }
                else if (state.CurrentPlatform == ConsoleNames.PS2)
                {
                    CreateConsoleFilesGh3Ps2(state);
                }
            }

            TimeSpan timeDiff = DateTime.Now - time1;
            string process = success ? "Chart compilation took" : "Compilation failed after";
            Console.WriteLine($"{process} {timeDiff.TotalSeconds.ToString("G3", Murica)} seconds");
            return new SongCompileResult { Success = success };
        }
        catch (OperationCanceledException)
        {
            return new SongCompileResult { Success = false, Cancelled = true };
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Compile Failed!", cancellationToken);
            return new SongCompileResult { Success = false, Error = ex };
        }
    }

    /// <summary>Exports a GH3 song as an .sgh archive (export flow used by the PAK button).</summary>
    public async Task<SongCompileResult> ExportSongArchiveAsync(
        SongProjectState state,
        CompileOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (state.CurrentGame != GAME_GH3)
        {
            await _notifications.ShowErrorAsync("Export Error",
                "Exporting song archives is currently only available for Guitar Hero III songs.", cancellationToken);
            return new SongCompileResult { Success = false };
        }

        var exportOptions = options with { IsExport = true };
        return await CompileAllAsync(state, exportOptions, progress, cancellationToken);
    }

    /// <summary>Compiles only the audio (menu action).</summary>
    public async Task CompileAudioOnlyAsync(
        SongProjectState state,
        bool encrypt,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report($"Compiling audio for {state.CurrentGame}");
        if (state.CurrentGame == GAME_GH3 || state.CurrentGame == GAME_GHA)
        {
            await CompileGh3AudioAsync(state, new CompileOptions { IsAudioCompile = true }, cancellationToken);
        }
        else
        {
            await CompileGhwtAudioAsync(state, new CompileOptions { IsAudioCompile = true }, encrypt, cancellationToken);
        }
    }

    // =====================================================================
    // Per-game PAK compilation
    // =====================================================================

    private async Task<bool> CompilePakGh3Async(SongProjectState state, CompileOptions options, CancellationToken ct, bool suppressMessages = false)
    {
        bool success = false;
        try
        {
            await PreChecksAsync(state, options, ct);
            CompileGh3PakFile(state, options);
            success = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            success = await HandleReadOnlyFailureAsync(ex, state, ct);
        }
        catch (MidiCompileException ex)
        {
            MidiFailException(ex);
        }
        catch (SkaFileParseException ex)
        {
            SkaFailException(ex);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Compile Failed!", ct, showDialog: !suppressMessages);
        }
        return success;
    }

    private async Task<bool> CompilePakGhwtAsync(SongProjectState state, CompileOptions options, CancellationToken ct, bool suppressMessages = false)
    {
        bool success = false;
        try
        {
            await PreChecksAsync(state, options, ct);
            ConvertLipsyncToWT(state);
            CompileGhwtPakFile(state, options);
            success = true;
        }
        catch (MidiCompileException ex)
        {
            MidiFailException(ex);
        }
        catch (SkaFileParseException ex)
        {
            SkaFailException(ex);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Compile Failed!", ct, showDialog: !suppressMessages);
        }
        return success;
    }

    private async Task<bool> CompilePakGh5Async(SongProjectState state, CompileOptions options, CancellationToken ct, bool suppressMessages = false)
    {
        bool success = false;
        try
        {
            await PreChecksAsync(state, options, ct);
            ConvertLipsyncToWT(state);
            CompileGh5PakFile(state, options);
            success = true;
        }
        catch (MidiCompileException ex)
        {
            MidiFailException(ex);
        }
        catch (SkaFileParseException ex)
        {
            SkaFailException(ex);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Compile Failed!", ct, showDialog: !suppressMessages);
        }
        return success;
    }

    private async Task PreChecksAsync(SongProjectState state, CompileOptions options, CancellationToken ct)
    {
        await PreCompileCheckAsync(state, options, ct);
        await DuplicateCheckAsync(state, ct);
        await _projects.SaveProjectAsync(state.Data, ct);
    }

    private async Task PreCompileCheckAsync(SongProjectState state, CompileOptions options, CancellationToken ct)
    {
        ConsoleStringCheck(state);
        AuthorCheck(state);

        string game = state.CurrentGame;
        if ((game == GAME_GH3 || game == GAME_GHA) && state.CurrentPlatform == ConsoleNames.PC)
        {
            await _checks.Gh3PcCheckAsync(game, cancellationToken: ct);
        }

        if (game == GAME_GHWT && state.CurrentPlatform == ConsoleNames.PC)
        {
            await _checks.CheckForModsFolderAsync(ct);
        }

        if (state.CurrentPlatform == ConsoleNames.PS2 && !options.IsExport)
        {
            CreateConsoleFolder(state);
        }
        else if (state.CurrentPlatform != ConsoleNames.PC)
        {
            await _checks.OnyxCheckAsync(ct);
            CreateConsoleFolder(state);
        }
        else if (options.IsExport)
        {
            CreateConsoleFolder(state);
        }
    }

    private void ConsoleStringCheck(SongProjectState state)
    {
        if (string.IsNullOrEmpty(state.Data.compilePath))
        {
            state.Data.compilePath = Path.GetDirectoryName(state.Data.projectPath) ?? "";
        }
        if (string.IsNullOrEmpty(state.Data.songName))
        {
            state.Data.songName = CreateChecksum(state.Data.title);
        }
        state.ConsoleCompile = Path.Combine(state.Data.compilePath, "Console");
    }

    private void AuthorCheck(SongProjectState state)
    {
        var stripXml = new System.Text.RegularExpressions.Regex(@"<[^>]+>");
        if (string.IsNullOrEmpty(state.Data.chartAuthor))
        {
            state.Data.chartAuthor = _platform.UserName;
        }
        state.Data.chartAuthor = stripXml.Replace(state.Data.chartAuthor, "");
    }

    private void CreateConsoleFolder(SongProjectState state)
    {
        if (Directory.Exists(state.ConsoleCompile))
        {
            Directory.Delete(state.ConsoleCompile, true);
        }
        Directory.CreateDirectory(state.ConsoleCompile);
    }

    private async Task DuplicateCheckAsync(SongProjectState state, CancellationToken ct)
    {
        var duplicateSet = ForbiddenChecksums.GetForbiddenChecksums(state.CurrentGame);
        if (!duplicateSet.Contains(state.Data.songName.ToLower()))
        {
            return;
        }

        if (Pref.ChecksumWarning == 0)
        {
            bool proceed = await _notifications.ConfirmAsync("Duplicate Checksum Warning",
                $"Warning: The song checksum \"{state.Data.songName}\" is restricted.\n\n" +
                "Press OK to continue, the checksum will be modified to prevent overwriting on-disc/included songs.\n\n" +
                "Alternatively, press Cancel to modify the checksum yourself.\n\n" +
                "(This warning can be silenced in Settings menu.)", ct);
            if (!proceed)
            {
                throw new OperationCanceledException("Compilation cancelled by user due to duplicate checksum.");
            }
        }
        else if (Pref.ChecksumWarning == 2)
        {
            throw new InvalidOperationException($"The song checksum \"{state.Data.songName}\" is restricted and cannot be used.");
        }
        state.Data.songName = "custom_" + state.Data.songName;
    }

    // =====================================================================
    // Song PAK compilers
    // =====================================================================

    private void CompileGh3PakFile(SongProjectState state, CompileOptions options)
    {
        string venue = GetVenue(state.Data.venueSourceGh3);
        string checksum = GetSongChecksum(state, options);
        bool gh3Plus = Pref.Gh3Plus && state.CurrentGame == GAME_GH3 && state.CurrentPlatform == ConsoleNames.PC;

        var compiler = new SongPakCompiler(
            midiPath: state.Data.midiPathGh3,
            savePath: state.Data.compilePath,
            songName: checksum,
            game: state.CurrentGame,
            gameConsole: state.CurrentPlatform)
        {
            HopoThreshold = state.Data.hmxHopoVal,
            SkaPath = state.Data.skaPathGh3,
            PerfOverride = state.Data.perfPathGh3,
            SongScripts = state.Data.songScriptPathGh3,
            SkaSource = GetSkaSource(state.Data.skaSourceGh3),
            VenueSource = venue,
            RhythmTrack = state.Data.isP2Rhythm,
            OverrideBeat = state.Data.useBeatTrack,
            HopoType = state.Data.hopoMode,
            IsSteven = GetVocalGenderText(state.Data.vocalGenderGh3) == "Steven Tyler",
            Gender = GetVocalGenderText(state.Data.vocalGenderGh3),
            Gh3Plus = gh3Plus
        };

        compiler.Build();
        state.EffectiveSongName = compiler.EffectiveSongName;

        var (pakFile, doubleKick, _) = compiler.GetResults();

        if (options.IsExport)
        {
            File.Move(pakFile, Path.Combine(state.ConsoleCompile, $"{state.EffectiveSongName}_song.pak"), true);
            var songEntry = GenerateGh3SongListEntry(state, options);
            QBItem songItem = new QBItem((string)songEntry["checksum"], songEntry);
            var saveQb = Path.Combine(state.ConsoleCompile, "songs.info");
            var bytes = QB.CompileQbFile([songItem], "songs.info", GAME_GH3, CONSOLE_XBOX);
            if (bytes == null)
            {
                Console.WriteLine("Failed to compile songs.info");
                return;
            }
            File.WriteAllBytes(saveQb, bytes);
        }
        else if (state.CurrentPlatform == ConsoleNames.PC)
        {
            AddToPCSetlist(state);
            MoveToGh3SongsFolder(state, pakFile);
        }
        else if (state.CurrentPlatform == ConsoleNames.PS2)
        {
            // Files are collected later by CreateConsoleFilesGh3Ps2
        }
        else
        {
            string dlcChecksum = Pref.DlcName
                ? $"dlc{state.ConsoleChecksum}_song.pak"
                : $"{checksum}_song.pak";
            File.Move(pakFile, Path.Combine(state.ConsoleCompile, dlcChecksum), true);
            if (!options.CompileToFolder)
            {
                CreateConsoleFilesGh3(state, options);
            }
            else
            {
                CreateConsoleFolderFilesGh3(state);
            }
        }
    }

    private void CompileGhwtPakFile(SongProjectState state, CompileOptions options)
    {
        var diffs = new Dictionary<string, int>
        {
            { "guitar", 1 },
            { "bass", 1 },
            { "drums", 1 },
            { "vocals", 1 }
        };

        string venue = GetVenue(state.Data.venueSource);

        var compiler = new SongPakCompiler(
            midiPath: state.Data.midiPath,
            savePath: state.Data.compilePath,
            songName: state.Data.songName,
            game: state.CurrentGame,
            gameConsole: state.CurrentPlatform)
        {
            HopoThreshold = state.Data.hmxHopoVal,
            SkaPath = state.Data.skaPath,
            PerfOverride = state.Data.perfPath,
            SongScripts = state.Data.songScriptPath,
            SkaSource = GAME_GHWT,
            VenueSource = venue,
            OverrideBeat = state.Data.useBeatTrack,
            HopoType = state.Data.hopoMode,
            EasyOpens = state.Data.easyOpen,
            Diffs = diffs
        };

        compiler.Build();

        var (pakFile, doubleKick, pakFileExPlus) = compiler.GetResults();
        state.WorldTourDiffs = compiler.Diffs ?? state.WorldTourDiffs;
        state.CompileExpertPlus = pakFileExPlus != null;

        if (state.CurrentPlatform == ConsoleNames.PC)
        {
            string modsFolder = Path.GetFullPath(Pref.WtModsFolder);
            if (!string.IsNullOrWhiteSpace(state.Data.modsSubfolder))
            {
                CheckModsSubfolder(state.Data.modsSubfolder);
                state.WtSongFolder = Path.Combine(modsFolder, state.Data.modsSubfolder, state.Data.songName);
                state.WtSongFolderExpertPlus = Path.Combine(modsFolder, state.Data.modsSubfolder, $"{state.Data.songName}X");
            }
            else
            {
                state.WtSongFolder = Path.Combine(modsFolder, state.Data.songName);
                state.WtSongFolderExpertPlus = Path.Combine(modsFolder, $"{state.Data.songName}X");
            }
            state.ContentFolder = Path.Combine(state.WtSongFolder, "Content");
            state.ContentFolderExpertPlus = Path.Combine(state.WtSongFolderExpertPlus, "Content");
            state.MusicFolder = Path.Combine(state.ContentFolder, "Music");
            state.MusicFolderExpertPlus = Path.Combine(state.ContentFolderExpertPlus, "Music");

            string pakName = Path.GetFileName(pakFile);
            Directory.CreateDirectory(state.MusicFolder);
            File.Move(pakFile, Path.Combine(state.ContentFolder, $"a{pakName}"), true);
            WriteWtdeIni(state.WtSongFolder, state, expertPlus: false);

            if (state.CompileExpertPlus)
            {
                Directory.CreateDirectory(state.MusicFolderExpertPlus);
                File.Move(pakFileExPlus!, Path.Combine(state.ContentFolderExpertPlus, $"a{Path.GetFileName(pakFileExPlus)}"), true);
                WriteWtdeIni(state.WtSongFolderExpertPlus, state, expertPlus: true);
            }
        }
    }

    private bool CompileGh5PakFile(SongProjectState state, CompileOptions options)
    {
        string venue = GetVenue(state.Data.venueSource);
        var diffs = new Dictionary<string, int>
        {
            { "guitar", state.Data.guitarTier },
            { "bass", state.Data.bassTier },
            { "drums", state.Data.drumsTier },
            { "vocals", state.Data.vocalsTier }
        };

        var compiler = new SongPakCompiler(
            midiPath: state.Data.midiPath,
            savePath: state.Data.compilePath,
            songName: GetSongChecksum(state, options),
            game: state.CurrentGame,
            gameConsole: state.CurrentPlatform)
        {
            HopoThreshold = state.Data.hmxHopoVal,
            SkaPath = state.Data.skaPath,
            PerfOverride = state.Data.perfPath,
            SongScripts = state.Data.songScriptPath,
            SkaSource = GAME_GHWT,
            VenueSource = venue,
            OverrideBeat = state.Data.useBeatTrack,
            HopoType = state.Data.hopoMode,
            EasyOpens = state.Data.easyOpen,
            Diffs = diffs
        };

        compiler.Build();

        (state.PakFilePath, var doubleKick, _) = compiler.GetResults();

        state.Data.guitarTier = compiler.Diffs?["guitar"] ?? state.Data.guitarTier;
        state.Data.bassTier = compiler.Diffs?["bass"] ?? state.Data.bassTier;
        state.Data.drumsTier = compiler.Diffs?["drums"] ?? state.Data.drumsTier;
        state.Data.vocalsTier = compiler.Diffs?["vocals"] ?? state.Data.vocalsTier;

        state.Metadata = PackageMetadataGhwtPlus(state, doubleKick);
        return true;
    }

    // =====================================================================
    // Audio compilation
    // =====================================================================

    private async Task CompileGh3AudioAsync(SongProjectState state, CompileOptions options, CancellationToken ct, bool suppressMessages = false)
    {
        int previewStart = state.PreviewStartTime;
        int previewLength = state.PreviewEndTime;
        if (state.Data.setEnd)
        {
            previewLength -= previewStart;
        }

        string[] backingPaths = SplitList(state.Data.backingPathsGh3);
        string[] coopBackingPaths = SplitList(state.Data.coopBackingPaths);

        string fileName = GetSongChecksum(state, options);

        var compiler = AudioCompiler.CreateGh3Compiler(
            fileName,
            state.Data.compilePath,
            "Compile",
            state.CurrentGame,
            previewStart,
            previewLength,
            state.Data.previewVolumeGh3,
            Pref.PreviewFadeIn,
            Pref.PreviewFadeOut,
            state.Data.useRenderedPreviewGh3,
            state.Data.isCoopAudio,
            state.Data.guitarPathGh3,
            state.Data.rhythmPathGh3,
            backingPaths,
            state.Data.crowdPathGh3,
            state.Data.previewAudioPathGh3,
            state.Data.coopGuitarPath,
            state.Data.coopRhythmPath,
            coopBackingPaths);

        try
        {
            if (state.CurrentPlatform == ConsoleNames.PS2)
            {
                await compiler.GH3AudioCompilePs2();
            }
            else
            {
                await compiler.GH3AudioCompile();
                var (fsbOut, datOut) = compiler.getFsbDat();
                if (options.IsExport || ((options.CompileToFolder || !Pref.DlcName) && state.CurrentPlatform != ConsoleNames.PC))
                {
                    File.Move(fsbOut, Path.Combine(state.ConsoleCompile, $"{fileName}.fsb"), true);
                    File.Move(datOut, Path.Combine(state.ConsoleCompile, $"{fileName}.dat"), true);
                }
                else if (options.IsAudioCompile)
                {
                    // Audio-only compile leaves the files in the compile folder
                }
                else if (state.CurrentPlatform == ConsoleNames.PC)
                {
                    MoveToGh3MusicFolder(state, fsbOut);
                    MoveToGh3MusicFolder(state, datOut);
                }
                else
                {
                    File.Move(fsbOut, Path.Combine(state.ConsoleCompile, $"dlc{state.ConsoleChecksum}.fsb"), true);
                    File.Move(datOut, Path.Combine(state.ConsoleCompile, $"dlc{state.ConsoleChecksum}.dat"), true);
                }
                Console.WriteLine("Audio Compilation Complete!");
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Audio Compilation Failed!", ct, showDialog: !suppressMessages);
            throw;
        }
    }

    private async Task CompileGhwtAudioAsync(SongProjectState state, CompileOptions options, bool encrypt, CancellationToken ct, bool suppressMessages = false)
    {
        int previewStart = state.PreviewStartTime;
        int previewLength = state.PreviewEndTime;
        if (state.Data.setEnd)
        {
            previewLength -= previewStart;
        }

        string[] backingPaths = SplitList(state.Data.backingPaths);

        string songChecksum = GetSongChecksum(state, options);

        var compiler = AudioCompiler.CreateGhwtCompiler(
            songChecksum,
            state.Data.compilePath,
            "Compile",
            state.CurrentGame,
            previewStart,
            previewLength,
            state.Data.previewVolume,
            Pref.PreviewFadeIn,
            Pref.PreviewFadeOut,
            state.Data.useRenderedPreview,
            state.Data.guitarPath,
            state.Data.bassPath,
            state.Data.vocalsPath,
            state.Data.kickPath,
            state.Data.snarePath,
            state.Data.cymbalsPath,
            state.Data.tomsPath,
            backingPaths,
            state.Data.crowdPath,
            state.Data.previewAudioPath);

        try
        {
            await compiler.GHWTAudioCompile(encrypt);
            var fsbList = compiler.getFsbList();
            if (state.CurrentPlatform == ConsoleNames.PC && !options.IsAudioCompile)
            {
                if (state.CompileExpertPlus)
                {
                    foreach (string fileEnd in new[] { "_1", "_2", "_3", "_preview" })
                    {
                        string file = Path.Combine(state.Data.compilePath, $"{songChecksum}{fileEnd}.fsb");
                        string fileName = Path.GetFileName(file);
                        string fileSave = Path.Combine(state.MusicFolder, $"{fileName}.xen");
                        string fileSaveExpertPlus = Path.Combine(state.MusicFolderExpertPlus, $"{songChecksum}X{fileEnd}.fsb.xen");

                        File.Copy(file, fileSave, true);
                        File.Move(file, fileSaveExpertPlus, true);
                    }
                }
                else
                {
                    foreach (string file in fsbList)
                    {
                        File.Move(file, Path.Combine(state.MusicFolder, $"{Path.GetFileName(file)}.xen"), true);
                    }
                }
            }
            Console.WriteLine("Audio Compilation Complete!");
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Audio Compilation Failed!", ct, showDialog: !suppressMessages);
            throw;
        }
    }

    private int GetAudioLength(SongProjectState state)
    {
        var fsb = new FSB();
        var backing = SplitList(state.Data.backingPaths);
        var allAudio = new List<string>
        {
            state.Data.kickPath, state.Data.snarePath, state.Data.cymbalsPath, state.Data.tomsPath,
            state.Data.guitarPath, state.Data.bassPath, state.Data.vocalsPath
        };
        allAudio.AddRange(backing);

        int duration = 0;
        foreach (string audio in allAudio)
        {
            try
            {
                var timespan = fsb.GetAudioDuration(audio);
                duration = Math.Max(duration, (int)Math.Round(timespan.TotalSeconds));
            }
            catch
            {
                // Missing/unknown audio is skipped (matches legacy behavior)
            }
        }
        return duration;
    }

    // =====================================================================
    // Console package creation
    // =====================================================================

    private void CreateConsoleFilesGh3(SongProjectState state, CompileOptions options)
    {
        string? overrideChecksum = !Pref.DlcName ? state.Data.songName : null;
        Directory.CreateDirectory(state.ConsoleCompile);
        CreateConsoleDownloadFilesGh3(state.ConsoleChecksum, state.CurrentGame, state.CurrentPlatform,
            state.ConsoleCompile, _resources.ResourcesPath, [GenerateGh3SongListEntry(state, options)], overrideChecksum);
    }

    private void CreateConsoleFolderFilesGh3(SongProjectState state)
    {
        CreateConsoleFolderGh3(GetSongChecksum(state, new CompileOptions { CompileToFolder = true }), state.CurrentGame,
            state.CurrentPlatform, state.ConsoleCompile, _resources.ResourcesPath,
            [GenerateGh3SongListEntry(state, new CompileOptions { CompileToFolder = true })]);
    }

    private void CreateConsoleFilesGh3Ps2(SongProjectState state)
    {
        var dummyText = "\\\\Dummy file - just here to fix dependencies.\r\n";
        var hopoOverride = state.Data.midiPathGh3.ToLower().EndsWith(".q") ? state.NsHopoVal : 500f;
        state.Metadata = PackageMetadata(state, hopoValOverride: hopoOverride);
        var songListEntry = state.Metadata.GenerateGh3SongListEntry(state.CurrentGame, state.CurrentPlatform);
        var setlistItem = new QBItem((string)songListEntry["checksum"], songListEntry);

        var ps2Compile = Path.Combine(state.Data.compilePath, "PS2 Compile");
        Directory.CreateDirectory(ps2Compile);
        var ps2SkaFiles = Path.Combine(state.Data.compilePath, "PS2 SKA Files");
        var ps2PakFile = Path.Combine(state.Data.compilePath, $"{state.Data.songName}.pak.ps2");
        var ps2Msv = Path.Combine(state.Data.compilePath, "output.msv");
        var ps2MsvCoop = Path.Combine(state.Data.compilePath, "output_coop.msv");
        var ps2MsvPreview = Path.Combine(state.Data.compilePath, "preview.msv");
        var spChecksum = CRC.QBKey(state.Data.songName).Replace("0x", "").ToUpper();
        var mpChecksum = CRC.QBKey($"{state.Data.songName}_coop").Replace("0x", "").ToUpper();
        var spPath = Path.Combine(ps2Compile, "MUSIC", spChecksum.Substring(0, 1));
        var mpPath = Path.Combine(ps2Compile, "MUSIC", mpChecksum.Substring(0, 1));

        var setlistSave = Path.Combine(ps2Compile, "setlist.q");
        QB.QbToText([setlistItem], setlistSave);

        if (Directory.Exists(ps2SkaFiles))
        {
            var finalSka = Path.Combine(ps2Compile, "WAD", "custom_songs", "ska", state.Metadata.Checksum);
            Directory.CreateDirectory(finalSka);
            foreach (var file in Directory.GetFiles(ps2SkaFiles))
            {
                var newSkaPath = Path.Combine(finalSka, Path.GetFileName(file));
                var relSkaPath = Path.GetRelativePath(Path.Combine(ps2Compile, "WAD"), newSkaPath);
                relSkaPath = relSkaPath.Substring(0, relSkaPath.IndexOf(".ps2", StringComparison.InvariantCultureIgnoreCase));
                state.Metadata.AddPs2ScriptEntry(relSkaPath);
                File.Move(file, newSkaPath, true);
            }
            var allanimsSave = Path.Combine(ps2Compile, "allanims.q");
            state.Metadata.SavePs2Script(allanimsSave);
            Directory.Delete(ps2SkaFiles);
        }

        if (File.Exists(ps2PakFile))
        {
            var songsFolder = Path.Combine(ps2Compile, "WAD", "songs");
            Directory.CreateDirectory(songsFolder);
            File.Move(ps2PakFile, Path.Combine(songsFolder, Path.GetFileName(ps2PakFile)), true);
            File.WriteAllText(Path.Combine(songsFolder, $"{state.Data.songName}_gfx.pak.ps2"), dummyText);
            File.WriteAllText(Path.Combine(songsFolder, $"{state.Data.songName}_sfx.pak.ps2"), dummyText);
        }
        if (File.Exists(ps2Msv))
        {
            Directory.CreateDirectory(spPath);
            File.Move(ps2Msv, Path.Combine(spPath, Path.GetFileName(ps2Msv).Replace("output.msv", $"{spChecksum}.IMF")), true);
        }
        if (File.Exists(ps2MsvCoop))
        {
            Directory.CreateDirectory(mpPath);
            File.Move(ps2MsvCoop, Path.Combine(mpPath, Path.GetFileName(ps2MsvCoop).Replace("output_coop.msv", $"{mpChecksum}.IMF")), true);
        }
        if (File.Exists(ps2MsvPreview))
        {
            Directory.CreateDirectory(spPath);
            File.Move(ps2MsvPreview, Path.Combine(spPath, Path.GetFileName(ps2Msv).Replace("output.msv", $"{spChecksum}.ISF")), true);
        }
    }

    private void CreateConsolePackage(SongProjectState state, CompileOptions options)
    {
        bool isGh3orGha = state.CurrentGame == GAME_GH3 || state.CurrentGame == GAME_GHA;
        if (isGh3orGha)
        {
            bool isImport = state.Data.midiPathGh3.ToLower().EndsWith(".q");
            float hopoValOverride = isImport ? state.NsHopoVal : 500f;
            state.Metadata = PackageMetadata(state, false, hopoValOverride, isImport);
        }

        if (!options.CompileToFolder)
        {
            state.Metadata.CreateConsolePackage(state.CurrentGame, state.CurrentPlatform, state.ConsoleCompile,
                _resources.ResourcesPath, Pref.OnyxCliPath);
        }
        else
        {
            bool isPS3 = state.CurrentPlatform == ConsoleNames.PS3;
            string ext = isPS3 ? ".PS3" : ".xen";
            foreach (var file in Directory.GetFiles(state.ConsoleCompile))
            {
                var fileExt = Path.GetExtension(file);
                if (fileExt.ToLower() == ".q")
                {
                    continue;
                }
                var newFile = Path.ChangeExtension(file, $"{fileExt}{ext}");
                if (isPS3)
                {
                    var fileName = Path.GetFileName(newFile).ToUpperInvariant();
                    newFile = Path.Combine(Path.GetDirectoryName(newFile), fileName);
                }
                File.Move(file, newFile, true);
            }

            string consoleFolder = Path.GetDirectoryName(state.ConsoleCompile) ?? "";
            if (!string.IsNullOrEmpty(consoleFolder) && Directory.Exists(consoleFolder))
            {
                string newConsoleFolder = Path.Combine(consoleFolder, "Song Compile");
                if (Directory.Exists(newConsoleFolder))
                {
                    Directory.Delete(newConsoleFolder, true);
                }
                Directory.Move(state.ConsoleCompile, newConsoleFolder);
            }
        }
    }

    private void MoveGh5Files(SongProjectState state)
    {
        Directory.CreateDirectory(state.ConsoleCompile);
        string currCheck = GetSongChecksum(state, new CompileOptions { CompileToFolder = true });
        int duration = 0;

        string pakCheck = $"{currCheck}_song.pak";
        string audioCheck = $"{currCheck}";

        if (currCheck.StartsWith("dlc"))
        {
            pakCheck = $"b{pakCheck}";
            audioCheck = $"a{audioCheck}";
        }

        File.Move(state.PakFilePath, Path.Combine(state.ConsoleCompile, pakCheck), true);
        for (int i = 1; i < 4; i++)
        {
            string audio = Path.Combine(state.Data.compilePath, $"{currCheck}_{i}.fsb");
            if (!File.Exists(audio))
            {
                throw new FileNotFoundException($"Missing audio file {audio} for download file creation.");
            }
            if (duration == 0)
            {
                var fileInfo = new FileInfo(audio);
                var length = fileInfo.Length - 128;
                duration = (int)Math.Round(length / 4 / 384 * 1152 / 48000f);
            }
            var encryptString = Path.GetFileNameWithoutExtension(audio);
            var encryptedAudio = EncryptDecrypt.EncryptFSB4(File.ReadAllBytes(audio), encryptString);
            var savePath = Path.Combine(state.ConsoleCompile, $"{audioCheck}_{i}.fsb");
            File.WriteAllBytes(savePath, encryptedAudio);
            File.Delete(audio);
        }
        state.Metadata.Duration = duration;

        var previewPath = Path.Combine(state.Data.compilePath, $"{currCheck}_preview.fsb");
        if (!File.Exists(previewPath))
        {
            throw new FileNotFoundException($"Missing preview audio file {previewPath} for download file creation.");
        }
        var previewSave = Path.Combine(state.ConsoleCompile, $"{audioCheck}_preview.fsb");
        var previewEncryptString = Path.GetFileNameWithoutExtension(previewPath);
        File.WriteAllBytes(previewSave, EncryptDecrypt.EncryptFSB4(File.ReadAllBytes(previewPath), previewEncryptString));
        File.Delete(previewPath);
    }

    // =====================================================================
    // Metadata helpers
    // =====================================================================

    private GhMetadata PackageMetadata(SongProjectState state, bool doubleKick = false, float hopoValOverride = 500f, bool gh3Convert = false)
    {
        return new GhMetadata
        {
            Checksum = GetSongChecksum(state, new CompileOptions()),
            CompileFolder = state.Data.compilePath,
            Title = state.Data.title,
            Artist = state.Data.artist,
            ArtistTextSelect = GetArtistTextSelect(state),
            ArtistTextCustom = state.Data.artistTextCustom,
            AlbumTitle = state.Data.album,
            Year = state.Data.songYear,
            CoverArtist = state.Data.coverArtist,
            CoverYear = state.Data.coverYear,
            Genre = GetGenre(state),
            ChartAuthor = state.Data.chartAuthor,
            Bassist = BassistName(state),
            Singer = SingerName(state),
            IsArtistFamousBy = IsArtistFamousBy(state),
            AerosmithBand = state.AerosmithBand,
            Beat8thLow = state.Data.beat8thLow,
            Beat8thHigh = state.Data.beat8thHigh,
            Beat16thLow = state.Data.beat16thLow,
            Beat16thHigh = state.Data.beat16thHigh,
            OverrideBeatLines = Pref.OverrideBeatLines,
            CoopAudioCheck = state.Data.isCoopAudio,
            P2RhythmCheck = state.Data.isP2Rhythm,
            BandVol = (float)state.Data.bandVolumeGh3,
            GtrVol = (float)state.Data.gtrVolumeGh3,
            Countoff = GetCountoffText(state.Data.countoffGh3),
            HopoThreshold = hopoValOverride,
            Gh3Convert = gh3Convert
        };
    }

    private GhMetadata PackageMetadataGhwtPlus(SongProjectState state, bool doubleKick = false)
    {
        return new GhMetadata
        {
            Checksum = GetSongChecksum(state, new CompileOptions()),
            CompileFolder = state.Data.compilePath,
            Title = $"\\L{state.Data.title}",
            Artist = $"\\L{state.Data.artist}",
            AlbumTitle = $"\\L{state.Data.album}",
            ArtistTextSelect = GetArtistTextSelect(state),
            ArtistTextCustom = state.Data.artistTextCustom,
            Year = state.Data.songYear,
            CoverArtist = state.Data.coverArtist,
            CoverYear = state.Data.coverYear,
            Genre = GetGenre(state),
            ChartAuthor = state.Data.chartAuthor,
            Singer = GetVocalGenderText(state.Data.vocalGender),
            IsArtistFamousBy = IsArtistFamousBy(state),
            Beat8thLow = state.Data.beat8thLow,
            Beat8thHigh = state.Data.beat8thHigh,
            Beat16thLow = state.Data.beat16thLow,
            Beat16thHigh = state.Data.beat16thHigh,
            OverrideBeatLines = Pref.OverrideBeatLines,
            BandVol = (float)state.Data.overallVolume,
            Countoff = GetCountoffText(state.Data.countoff),
            DoubleKick = doubleKick,
            VocalTuningCents = (int)state.Data.vocalTuningCents,
            SustainThreshold = (float)state.Data.sustainThreshold,
            VocalScrollSpeed = (float)state.Data.vocalScrollSpeed,
            GuitarMic = state.Data.guitarMic,
            BassMic = state.Data.bassMic,
            DrumKit = state.Data.ghwtDrumkit,
            Duration = GetAudioLength(state),
            Game = state.CurrentGame,
            GuitarTier = state.Data.guitarTier,
            BassTier = state.Data.bassTier,
            DrumsTier = state.Data.drumsTier,
            VocalsTier = state.Data.vocalsTier,
            BandTier = state.Data.bandTier
        };
    }

    private QBStruct.QBStructData GenerateGh3SongListEntry(SongProjectState state, CompileOptions options)
    {
        state.IsImport = state.Data.midiPathGh3.ToLower().EndsWith(".q");
        float hopoValOverride = state.IsImport ? state.NsHopoVal : 500f;

        var songListEntry = PackageMetadata(state, false, hopoValOverride, state.IsImport);
        return songListEntry.GenerateGh3SongListEntry(state.CurrentGame, state.CurrentPlatform);
    }

    private string GetGenre(SongProjectState state)
    {
        return state.CurrentGame switch
        {
            GAME_GHWT => Genres.Wt[Math.Clamp(state.Data.wtGenre, 0, Genres.Wt.Count - 1)],
            GAME_GH5 => Genres.Gh5[Math.Clamp(state.Data.gh5Genre, 0, Genres.Gh5.Count - 1)],
            GAME_GHWOR => Genres.Wor[Math.Clamp(state.Data.worGenre, 0, Genres.Wor.Count - 1)],
            _ => "Rock"
        };
    }

    private string GetArtistTextSelect(SongProjectState state) => ArtistsText[Math.Clamp(state.Data.artistText, 0, ArtistsText.Length - 1)];

    private bool IsArtistFamousBy(SongProjectState state) => GetArtistTextSelect(state) == "As Made Famous By";

    private string BassistName(SongProjectState state)
    {
        if (state.CurrentGame == GAME_GHA)
        {
            return "Default";
        }
        string bassist = Bassists[Math.Clamp(state.Data.bassistSelect, 0, Bassists.Length - 1)];
        return bassist switch
        {
            "Tom Morello" => "Morello",
            "Lou" => "Satan",
            "God of Rock/Metalhead" => state.CurrentPlatform == ConsoleNames.PS2 ? "Metalhead" : "RockGod",
            "Grim Ripper/Elroy" => state.CurrentPlatform == ConsoleNames.PS2 ? "Elroy" : "Ripper",
            _ => bassist
        };
    }

    private string SingerName(SongProjectState state)
    {
        string singer = GetVocalGenderText(state.Data.vocalGenderGh3);
        return singer == "Bret Michaels" ? "Bret" : singer;
    }

    private void WriteWtdeIni(string saveFolder, SongProjectState state, bool expertPlus = false)
    {
        var config = new IniParserConfiguration { AssigmentSpacer = "" };
        var noSpaceParser = new IniParser.Parser.IniDataParser(config);
        var parser = new FileIniDataParser(noSpaceParser);
        var ini = GenerateWtdeIni(state, expertPlus);
        var iniPath = Path.Combine(saveFolder, "song.ini");
        parser.WriteFile(iniPath, ini);
    }

    private IniData GenerateWtdeIni(SongProjectState state, bool expertPlus = false)
    {
        var config = new IniParserConfiguration { AssigmentSpacer = "" };
        bool cover = state.Data.isCover;

        IniData wtdeIni = new IniData { Configuration = config };
        var modInfo = new SectionData("ModInfo");
        var songName = expertPlus ? $"{state.Data.songName}X" : state.Data.songName;
        var songTitle = expertPlus ? $"{state.Data.title} (Expert+)" : state.Data.title;
        modInfo.Keys.AddKey("Name", songName);
        modInfo.Keys.AddKey("Description", "Generated with Addy's Song Compiler");
        modInfo.Keys.AddKey("Author", state.Data.chartAuthor);
        modInfo.Keys.AddKey("Version", "1");

        var songInfo = new SectionData("SongInfo");
        songInfo.Keys.AddKey("Checksum", songName);
        songInfo.Keys.AddKey("Title", songTitle);
        songInfo.Keys.AddKey("Artist", state.Data.artist);
        songInfo.Keys.AddKey("Year", state.Data.songYear.ToString("G0", Murica));
        songInfo.Keys.AddKey("ArtistText", GetArtistText(state));
        if (cover)
        {
            songInfo.Keys.AddKey("CoverArtist", state.Data.coverArtist);
            songInfo.Keys.AddKey("CoverYear", state.Data.coverYear.ToString("G0", Murica));
        }
        songInfo.Keys.AddKey("OriginalArtist", cover ? "0" : "1");
        songInfo.Keys.AddKey("Leaderboard", "1");
        songInfo.Keys.AddKey("Singer", GetVocalGenderText(state.Data.vocalGender));
        songInfo.Keys.AddKey("Genre", GetGenre(state));
        songInfo.Keys.AddKey("Countoff", GetCountoffText(state.Data.countoff));
        songInfo.Keys.AddKey("Volume", state.Data.overallVolume.ToString("G2", Murica));

        if (!string.IsNullOrEmpty(state.Data.gameIcon)) songInfo.Keys.AddKey("GameIcon", state.Data.gameIcon);
        if (!string.IsNullOrEmpty(state.Data.gameCategory)) songInfo.Keys.AddKey("GameCategory", state.Data.gameCategory);
        if (!string.IsNullOrEmpty(state.Data.bandWtde)) songInfo.Keys.AddKey("Band", state.Data.bandWtde);
        if (state.Data.useNewClips) songInfo.Keys.AddKey("UseNewClips", "1");
        if (!string.Equals(state.Data.bSkeleton, "Default", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(state.Data.bSkeleton))
            songInfo.Keys.AddKey("SkeletonTypeB", state.Data.bSkeleton);
        if (!string.Equals(state.Data.dSkeleton, "Default", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(state.Data.dSkeleton))
            songInfo.Keys.AddKey("SkeletonTypeD", state.Data.dSkeleton);
        if (!string.Equals(state.Data.gSkeleton, "Default", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(state.Data.gSkeleton))
            songInfo.Keys.AddKey("SkeletonTypeG", state.Data.gSkeleton);
        if (!string.Equals(state.Data.vSkeleton, "Default", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(state.Data.vSkeleton))
            songInfo.Keys.AddKey("SkeletonTypeV", state.Data.vSkeleton);
        if (state.Data.bassMic) songInfo.Keys.AddKey("MicForBassist", "1");
        if (state.Data.guitarMic) songInfo.Keys.AddKey("MicForGuitarist", "1");

        string drumKit = GetDrumKit(state).Replace(" ", "").ToLower();
        songInfo.Keys.AddKey("DrumKit", drumKit);

        if (Pref.OverrideBeatLines)
        {
            songInfo.Keys.AddKey("Low8Bars", state.Data.beat8thLow.ToString());
            songInfo.Keys.AddKey("High8Bars", state.Data.beat8thHigh.ToString());
            songInfo.Keys.AddKey("Low16Bars", state.Data.beat16thLow.ToString());
            songInfo.Keys.AddKey("High16Bars", state.Data.beat16thHigh.ToString());
        }
        songInfo.Keys.AddKey("Cents", ((int)state.Data.vocalTuningCents).ToString("G0", Murica));
        songInfo.Keys.AddKey("WhammyCutoff", state.Data.sustainThreshold.ToString("G2", Murica));
        songInfo.Keys.AddKey("VocalsScrollSpeed", state.Data.vocalScrollSpeed.ToString("G2", Murica));
        if (state.Data.modernStrobes) songInfo.Keys.AddKey("ModernStrobes", "1");

        if (expertPlus)
        {
            songInfo.Keys.AddKey("HasDoubleBass", "1");
            songInfo.Keys.AddKey("HideInSetlistG", "1");
            songInfo.Keys.AddKey("HideInSetlistB", "1");
            songInfo.Keys.AddKey("HideInSetlistV", "1");
        }
        else
        {
            int numZeroDiffs = 0;
            foreach (string inst in state.WorldTourDiffs.Keys)
            {
                if (state.WorldTourDiffs[inst] == 0)
                {
                    songInfo.Keys.AddKey($"HideInSetlist{inst.ToUpper()[0]}", "1");
                    numZeroDiffs++;
                }
            }
            if (numZeroDiffs == 3)
            {
                songInfo.Keys.AddKey("HideInSetlistA", "1");
            }
        }

        if (state.Data.guitarCareerTier > 0) songInfo.Keys.AddKey("CareerSortIndexG", state.Data.guitarCareerTier.ToString());
        if (state.Data.bassCareerTier > 0) songInfo.Keys.AddKey("CareerSortIndexB", state.Data.bassCareerTier.ToString());
        if (state.Data.vocalsCareerTier > 0) songInfo.Keys.AddKey("CareerSortIndexV", state.Data.vocalsCareerTier.ToString());
        if (state.Data.drumsCareerTier > 0) songInfo.Keys.AddKey("CareerSortIndexD", state.Data.drumsCareerTier.ToString());
        if (state.Data.bandCareerTier > 0) songInfo.Keys.AddKey("CareerSortIndexA", state.Data.bandCareerTier.ToString());

        wtdeIni.Sections.Add(modInfo);
        wtdeIni.Sections.Add(songInfo);

        return wtdeIni;
    }

    private string GetArtistText(SongProjectState state)
    {
        bool artistIsOther = GetArtistTextSelect(state) == "Other";
        return !artistIsOther ? $"artist_text_{GetArtistTextSelect(state).ToLower().Replace(" ", "_")}" : state.Data.artistTextCustom;
    }

    private string GetDrumKit(SongProjectState state)
    {
        return state.CurrentGame switch
        {
            GAME_GHWT => string.IsNullOrEmpty(state.Data.ghwtDrumkit) ? "Modern Rock" : state.Data.ghwtDrumkit,
            GAME_GH5 => string.IsNullOrEmpty(state.Data.gh5Drumkit) ? "Modern Rock" : state.Data.gh5Drumkit,
            GAME_GHWOR => string.IsNullOrEmpty(state.Data.ghworDrumkit) ? "Modern Rock" : state.Data.ghworDrumkit,
            _ => "Modern Rock"
        };
    }

    // =====================================================================
    // Destination helpers
    // =====================================================================

    private void AddToPCSetlist(SongProjectState state)
    {
        var songListEntry = GenerateGh3SongListEntry(state, new CompileOptions());
        var gameFolder = _checks.GetCustomsPak(state.CurrentGame);
        var (pakData, pabData) = CreateForGame.AddToDownloadList(gameFolder, state.CurrentPlatform, [songListEntry], state.CurrentGame);
        _checks.OverwriteGh3Pak(pakData, pabData!, state.CurrentGame);
    }

    private void MoveToGh3SongsFolder(SongProjectState state, string pakPath)
    {
        string gameFolder = _checks.GetGh3Folder(state.CurrentGame);
        string saveFolder = Path.Combine(gameFolder, GameConstants.DATA, GameConstants.SONGS);
        string savePath = Path.Combine(saveFolder, Path.GetFileName(pakPath));
        Directory.CreateDirectory(saveFolder);
        File.Move(pakPath, savePath, true);
    }

    private void MoveToGh3MusicFolder(SongProjectState state, string audioPath)
    {
        string gameFolder = _checks.GetGh3Folder(state.CurrentGame);
        string saveFolder = Path.Combine(gameFolder, GameConstants.DATA, GameConstants.MUSIC);
        string savePath = Path.Combine(saveFolder, Path.GetFileName(audioPath));
        if (!savePath.EndsWith(".xen"))
        {
            savePath += ".xen";
        }
        Directory.CreateDirectory(saveFolder);
        File.Move(audioPath, savePath, true);
    }

    // =====================================================================
    // Checksum / naming helpers
    // =====================================================================

    public static string CreateChecksum(string title)
    {
        string formD = title.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char ch in formD)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        string alphanumericOnly = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "[^A-Za-z]", "").ToLower();
        return alphanumericOnly;
    }

    private string GetSongChecksum(SongProjectState state, CompileOptions options)
    {
        bool isGh3orGha = state.CurrentGame == GAME_GH3 || state.CurrentGame == GAME_GHA;
        bool alwaysName = state.CurrentPlatform == ConsoleNames.PC || state.CurrentPlatform == ConsoleNames.PS2;

        if (!isGh3orGha && !options.CompileToFolder && !alwaysName)
        {
            return $"dlc{state.ConsoleChecksum}";
        }
        else if (alwaysName || !Pref.DlcName || options.IsAudioCompile)
        {
            return state.EffectiveSongName;
        }
        else
        {
            return $"dlc{state.ConsoleChecksum}";
        }
    }

    private string GetVenue(int venueType) => venueType switch
    {
        0 => "GH3",
        1 => "GHA",
        _ => "GHWT"
    };

    public void SetConsoleChecksum(SongProjectState state)
    {
        string qbString = $"{state.CurrentGame}{state.Data.artist}{state.Data.title}{state.Data.songYear}{state.Data.chartAuthor}{state.Data.isCover}";
        state.ConsoleChecksum = CreateForGame.MakeConsoleChecksum([qbString]);
    }

    private string GetSkaSource(int skaSource) => skaSource switch
    {
        2 => "GH3",
        1 => "GHA",
        _ => "GHWT"
    };

    private void ConvertLipsyncToWT(SongProjectState state)
    {
        string lipSyncPath = state.Data.lipsyncPath;
        string skaPath = state.Data.skaPath;

        if (string.IsNullOrEmpty(lipSyncPath) || !Directory.Exists(lipSyncPath))
        {
            return;
        }

        if (!Directory.Exists(state.Data.skaPath))
        {
            state.Data.skaPath = lipSyncPath + "-temp";
            skaPath = state.Data.skaPath;
            Directory.CreateDirectory(skaPath);
        }

        float multiplier = state.Data.skaSource;
        if (multiplier == 0)
        {
            multiplier = 1f;
        }

        foreach (string file in Directory.GetFiles(lipSyncPath, "*.ska*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(lipSyncPath, file);
            string skaFile = Path.Combine(skaPath, relPath);
            var skaBytes = new SkaFile(file, "big");
            if (File.Exists(skaFile))
            {
                File.Delete(skaFile);
            }
            File.WriteAllBytes(skaFile, skaBytes.WriteModernStyleSka(SKELETON_WT_ROCKER, state.CurrentGame, multiplier));
        }
    }

    private void CheckModsSubfolder(string subfolder)
    {
        string modsFolder = Path.GetFullPath(Pref.WtModsFolder);
        string inputPath;

        if (Path.IsPathRooted(subfolder))
        {
            inputPath = Path.GetFullPath(subfolder);
            if (!inputPath.StartsWith(modsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("The subfolder path must be within the MODS folder.");
            }
        }
        else
        {
            inputPath = Path.GetFullPath(Path.Combine(modsFolder, subfolder));
            if (!inputPath.StartsWith(modsFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Invalid relative MODS folder path. It escapes the MODS folder.");
            }
        }
    }

    // =====================================================================
    // Error handling
    // =====================================================================

    private async Task<bool> HandleReadOnlyFailureAsync(UnauthorizedAccessException ex, SongProjectState state, CancellationToken ct)
    {
        string errorMessage = "Compilation has failed due to one or more files having \"Read-Only\" permission. " +
            "To be able to compile, the Toolkit needs to remove the read-only restriction on your game folder.\n\n" +
            "Press OK to attempt this automatically, or Cancel to do it yourself and re-compile afterwards.";
        bool attempt = await _notifications.ConfirmAsync("Could not access file", errorMessage, ct);
        if (!attempt)
        {
            await HandleExceptionAsync(ex, "Compile Failed!", ct);
            return false;
        }

        string gamePath = _checks.GetGh3Folder(state.CurrentGame);
        if (_platform.IsWindows && File.Exists(_resources.RemoveReadOnlyToolPath!))
        {
            try
            {
                await _processes.RunElevatedAsync(_resources.RemoveReadOnlyToolPath!, gamePath, ct);
                await _notifications.ShowMessageAsync("RemoveReadOnly Success",
                    "Read-Only permissions have been removed from the game folder. Please re-compile the song.", ct);
            }
            catch (Exception ex2)
            {
                await _notifications.ShowErrorAsync("RemoveReadOnly Failed!", ex2.Message, ct);
            }
        }
        else
        {
            bool fixed_ = await _permissions.TryMakeWritableRecursiveAsync(gamePath, cancellationToken: ct);
            if (fixed_)
            {
                await _notifications.ShowMessageAsync("Permissions Updated",
                    "Write permission has been added to the game folder. Please re-compile the song.", ct);
            }
            else
            {
                await _notifications.ShowErrorAsync("Permissions Update Failed",
                    "Could not update folder permissions automatically.\n\n" +
                    "On Linux you can fix this by running:\n  chmod -R u+w \"" + gamePath + "\"\n\n" +
                    "Please re-compile afterwards.", ct);
            }
        }
        return false;
    }

    private async Task HandleExceptionAsync(Exception ex, string title, CancellationToken ct, bool showDialog = true)
    {
        Console.WriteLine($"Exception in {title}: {ex}");
        if (showDialog)
        {
            await _notifications.ShowErrorAsync(title,
                $"Exception:\n\n{ex.Message}\n\nDetails have been written to the log on the main window.", ct);
        }
    }

    private static void MidiFailException(Exception ex)
    {
        Console.WriteLine("Errors were found while compiling the MIDI:");
        Console.WriteLine(ex.Message);
        Console.WriteLine("Compilation has been cancelled.");
    }

    private static void SkaFailException(Exception ex)
    {
        Console.WriteLine("Errors were found while processing the SKA files:");
        Console.WriteLine(ex.Message);
        Console.WriteLine("Compilation has been cancelled.");
    }

    // =====================================================================
    // Static option lists (ported from the WinForms designer)
    // =====================================================================

    public static readonly string[] ArtistsText = ["By", "As Made Famous By", "Other"];
    public static readonly string[] Countoffs = ["HiHat01", "HiHat02", "HiHat03", "Sticks_Huge", "Sticks_Normal", "Sticks_Tiny"];
    public static readonly string[] VocalGenders = ["Male", "Female", "None", "Bret Michaels", "Steven Tyler"];
    public static readonly string[] Bassists =
    [
        "Default", "Axel", "Casey", "Izzy", "Judy", "Johnny", "Lars", "Midori", "Xavier", "Slash",
        "Tom Morello", "Lou", "God of Rock/Metalhead", "Grim Ripper/Elroy"
    ];
    public static readonly string[] Venues = ["Guitar Hero 3", "Guitar Hero: Aerosmith", "Guitar Hero World Tour"];
    public static readonly string[] SkaSources = ["Guitar Hero World Tour+/Blender Export", "Guitar Hero: Aerosmith", "Guitar Hero 3"];
    public static readonly string[] HopoModes = ["Rock Band", "Moonscraper", "Guitar Hero 3", "Guitar Hero: World Tour+"];
    public static readonly string[] AerosmithBands =
    [
        "aerosmith_band", "aerosmith_band_backinthesaddle", "aerosmith_band_beyondbeautiful", "aerosmith_band_brightlightfright",
        "aerosmith_band_combination", "aerosmith_band_drawtheline", "aerosmith_band_dreamon", "aerosmith_band_joeperrybossbattle",
        "aerosmith_band_kingsandqueens", "aerosmith_band_letthemusicdothetalkin", "aerosmith_band_livinontheedge",
        "aerosmith_band_loveinanelevator", "aerosmith_band_makeit", "aerosmith_band_mamakin", "aerosmith_band_mercy",
        "aerosmith_band_miracas", "aerosmith_band_movinout", "aerosmith_band_nobodysfault", "aerosmith_band_nosurprize",
        "aerosmith_band_pandorasbox", "aerosmith_band_pink", "aerosmith_band_ragdoll", "aerosmith_band_ratsinthecellar",
        "aerosmith_band_shakinmycage", "aerosmith_band_sweetemotion", "aerosmith_band_talktalkin", "aerosmith_band_toysintheattic",
        "aerosmith_band_trainkeptarollin", "aerosmith_band_unclesalty", "aerosmith_band_walkthisway", "aerosmith_band_walkthiswayDMC"
    ];

    public static string GetCountoffText(int index) => Countoffs[Math.Clamp(index, 0, Countoffs.Length - 1)];
    public static string GetVocalGenderText(int index) => VocalGenders[Math.Clamp(index, 0, VocalGenders.Length - 1)];
    public static string GetBassistText(int index) => Bassists[Math.Clamp(index, 0, Bassists.Length - 1)];

    private static string[] SplitList(string? joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split(';', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>Genre lists per game (ported from the legacy SetGenres logic).</summary>
public static class Genres
{
    public static readonly List<string> Base =
    [
        "Rock", "Punk", "Glam Rock", "Black Metal", "Classic Rock", "Pop"
    ];

    private static readonly string[] WtRaw = ["Heavy Metal", "Goth"];
    private static readonly string[] Gh5Raw =
    [
        "Alternative", "Big Band", "Blues", "Blues Rock", "Country", "Dance", "Death Metal", "Disco",
        "Electronic", "Experimental", "Funk", "Grunge", "Hard Rock", "Hardcore", "Hip Hop", "Indie Rock",
        "Industrial", "International", "Jazz", "Metal", "Modern Rock", "New Wave", "Nu Metal", "Pop Punk",
        "Pop Rock", "Prog Rock", "R&B", "Reggae", "Rockabilly", "Ska Punk", "Southern Rock", "Speed Metal",
        "Surf Rock"
    ];
    private static readonly string[] Gh6Raw = ["Hardcore Punk", "Heavy Metal", "Progressive Rock"];

    public static readonly List<string> Wt = Build(WtRaw);
    public static readonly List<string> Gh5 = Build(Gh5Raw);
    public static readonly List<string> Wor = Build(Gh5Raw.Concat(Gh6Raw));

    private static List<string> Build(IEnumerable<string> extra)
    {
        var merged = new List<string>(Base);
        merged.AddRange(extra);
        merged.Sort();
        merged.Add("Other");
        return merged;
    }
}

/// <summary>Result of a compilation run.</summary>
public sealed class SongCompileResult
{
    public bool Success { get; init; }
    public bool Cancelled { get; init; }
    public Exception? Error { get; init; }
}
