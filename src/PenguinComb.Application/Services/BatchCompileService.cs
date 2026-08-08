using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Models;

namespace PenguinComb.Application.Services;

/// <summary>Outcome of compiling a single song in a batch run.</summary>
public sealed record BatchSongResult(string SourcePath, string SongName, bool Success, string? Error, bool Cancelled);

/// <summary>Progress reported between songs during a batch run.</summary>
public sealed record BatchCompileUpdate(int Completed, int Total, string CurrentSong, string Status);

/// <summary>Kind of input a batch entry represents.</summary>
public enum BatchSourceKind
{
    /// <summary>A .ghproj project file.</summary>
    Project,
    /// <summary>A Clone Hero song folder (song.ini + chart + audio).</summary>
    CloneHeroFolder
}

/// <summary>One entry in a batch run: either a .ghproj path or a Clone Hero folder.</summary>
public sealed record BatchSource(BatchSourceKind Kind, string Path);

/// <summary>
/// Compiles a list of songs sequentially through the same pipeline as the
/// single-song compiler. Sources can be .ghproj project files or Clone Hero song
/// folders (which are imported into a GH3 PC project first). A failure in one song
/// does not stop the batch; every entry gets a result and errors are reported in
/// the result list instead of modal dialogs.
/// </summary>
public class BatchCompileService
{
    private readonly ProjectFileService _projects;
    private readonly SongCompileService _compile;
    private readonly IAppDataLocator _appData;
    private readonly ISettingsService _settings;
    private readonly GameInstallValidator _validator;

    public BatchCompileService(
        ProjectFileService projects,
        SongCompileService compile,
        IAppDataLocator appData,
        ISettingsService settings,
        GameInstallValidator validator)
    {
        _projects = projects;
        _compile = compile;
        _appData = appData;
        _settings = settings;
        _validator = validator;
    }

    /// <summary>Compiles .ghproj projects (kept for the file-based batch flow).</summary>
    public Task<IReadOnlyList<BatchSongResult>> CompileAllAsync(
        IReadOnlyList<string> projectPaths,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
        => CompileAsync(projectPaths.Select(p => new BatchSource(BatchSourceKind.Project, p)).ToList(),
            progress, cancellationToken);

    /// <summary>Imports and compiles Clone Hero song folders as GH3 PC songs.</summary>
    /// <param name="nameSuffix">Optional text appended to the end of every imported
    /// song's name and title (e.g. "GH 2" becomes "My Song - GH 2").</param>
    public Task<IReadOnlyList<BatchSongResult>> CompileChFoldersAsync(
        IReadOnlyList<string> folders,
        string? nameSuffix = null,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
        => CompileAsync(folders.Select(f => new BatchSource(BatchSourceKind.CloneHeroFolder, f)).ToList(),
            progress, cancellationToken, nameSuffix);

    /// <summary>
    /// Quick "Clone Hero to Better GH3" flow: validates and remembers the game
    /// folder, then imports and compiles every Clone Hero song. No .ghproj files
    /// are kept - each song is compiled straight from its folder.
    /// </summary>
    /// <exception cref="InvalidOperationException">The GH3 folder is not a valid
    /// game installation (missing data files).</exception>
    public async Task<IReadOnlyList<BatchSongResult>> CompileChToGh3Async(
        IReadOnlyList<string> folders,
        string gh3Folder,
        string? nameSuffix = null,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(gh3Folder, GameNames.GH3);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"The selected folder is not a valid Guitar Hero 3 installation. " +
                $"Missing: {string.Join(", ", validation.MissingItems)}");
        }

        // Remember the game folder so the compile preflight does not re-prompt.
        _settings.Settings.Gh3FolderPath = gh3Folder;
        await _settings.SaveAsync(cancellationToken);

        var results = new List<BatchSongResult>(folders.Count);
        int completed = 0;

        foreach (string folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string folderName = DisplayName(folder);
            try
            {
                progress?.Report(new BatchCompileUpdate(completed, folders.Count, folderName, "Importing Clone Hero folder..."));
                var data = CreateChProject(folder, out string songName, nameSuffix, saveProject: false);
                var state = CreateState(data);

                var result = await CompileProjectAsync(state, folder, songName,
                    completed, folders.Count, progress, cancellationToken);

                // The transient project file was only needed so the pipeline's
                // auto-save does not prompt; remove it now.
                try { File.Delete(data.projectPath); } catch { }

                results.Add(result);
                if (result.Cancelled)
                {
                    return results;
                }
            }
            catch (OperationCanceledException)
            {
                results.Add(new BatchSongResult(folder, folderName, false, "Cancelled", true));
                return results;
            }
            catch (Exception ex)
            {
                results.Add(new BatchSongResult(folder, folderName, false, ex.Message, false));
            }

            completed++;
        }

        return results;
    }

    /// <summary>
    /// Compiles every source in order. Cancellation stops the batch after the
    /// current song; already-processed songs keep their results.
    /// </summary>
    public async Task<IReadOnlyList<BatchSongResult>> CompileAsync(
        IReadOnlyList<BatchSource> sources,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default,
        string? nameSuffix = null)
    {
        var results = new List<BatchSongResult>(sources.Count);
        int completed = 0;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string displayName = source.Kind == BatchSourceKind.CloneHeroFolder
                ? DisplayName(source.Path)
                : Path.GetFileNameWithoutExtension(source.Path);
            try
            {
                SongProjectData data;
                string songName;

                if (source.Kind == BatchSourceKind.CloneHeroFolder)
                {
                    progress?.Report(new BatchCompileUpdate(completed, sources.Count, displayName, "Importing Clone Hero folder..."));
                    data = CreateChProject(source.Path, out songName, nameSuffix, saveProject: true);
                }
                else
                {
                    progress?.Report(new BatchCompileUpdate(completed, sources.Count, displayName, "Loading project..."));
                    data = await _projects.LoadProjectAsync(source.Path, cancellationToken);
                    if (data is null)
                    {
                        results.Add(new BatchSongResult(source.Path, displayName, false, "Could not read the project file.", false));
                        completed++;
                        continue;
                    }
                    songName = data.songName;
                }

                var state = CreateState(data);
                var result = await CompileProjectAsync(state, source.Path, songName,
                    completed, sources.Count, progress, cancellationToken);
                results.Add(result);
                if (result.Cancelled)
                {
                    return results;
                }
            }
            catch (OperationCanceledException)
            {
                results.Add(new BatchSongResult(source.Path, displayName, false, "Cancelled", true));
                return results;
            }
            catch (Exception ex)
            {
                results.Add(new BatchSongResult(source.Path, displayName, false, ex.Message, false));
            }

            completed++;
        }

        return results;
    }

    private async Task<BatchSongResult> CompileProjectAsync(
        SongProjectState state,
        string sourcePath,
        string songName,
        int completed,
        int total,
        IProgress<BatchCompileUpdate>? progress,
        CancellationToken ct)
    {
        progress?.Report(new BatchCompileUpdate(completed, total, songName, "Compiling..."));
        try
        {
            SongCompileResult result;
            _projects.SetAllToAbsolute(state.Data);
            try
            {
                // suppressMessages: batch failures are reported per song in the
                // result list and the summary, not as interrupting modal dialogs.
                result = await _compile.CompileAllAsync(state,
                    new CompileOptions { ShowPostCompile = false },
                    progress: null,
                    ct,
                    suppressMessages: true);
            }
            finally
            {
                _projects.SetAllToRelative(state.Data);
            }

            if (result.Cancelled)
            {
                return new BatchSongResult(sourcePath, songName, false, "Cancelled", true);
            }

            return new BatchSongResult(sourcePath, songName, result.Success,
                result.Error?.Message ?? (result.Success ? null : "Compilation failed."), false);
        }
        catch (OperationCanceledException)
        {
            return new BatchSongResult(sourcePath, songName, false, "Cancelled", true);
        }
        catch (Exception ex)
        {
            return new BatchSongResult(sourcePath, songName, false, ex.Message, false);
        }
    }

    /// <summary>
    /// Builds a GH3 PC project from a Clone Hero song folder (song.ini metadata +
    /// chart + audio). When <paramref name="saveProject"/> is true the project is
    /// recorded as a .ghproj in the per-user data directory; otherwise a transient
    /// project file is used so the compile pipeline's auto-save writes there
    /// instead of prompting, and the caller removes it afterwards.
    /// </summary>
    private SongProjectData CreateChProject(string folder, out string songName, string? nameSuffix, bool saveProject)
    {
        var data = _projects.LoadProjectSync(_projects.DefaultTemplatePath) ?? new SongProjectData();
        _projects.LoadFromChFolder(data, folder);

        // Target GH3 PC: this is the Clone Hero -> GH3 conversion flow.
        data.gameSelect = "GH3";
        data.platformSelect = "PC";

        string folderName = DisplayName(folder);
        songName = string.IsNullOrWhiteSpace(data.songName) ? folderName : data.songName;

        // Optional source tag appended to the end of the name, e.g. "GH 2" makes
        // "My Song - GH 2". Applied to both the short name (keeps checksums
        // distinct per source) and the display title (visible in-game).
        if (!string.IsNullOrWhiteSpace(nameSuffix))
        {
            songName = ApplyNameSuffix(songName, nameSuffix);
            if (!string.IsNullOrWhiteSpace(data.title))
            {
                data.title = ApplyNameSuffix(data.title, nameSuffix);
            }
        }
        data.songName = songName;

        if (saveProject)
        {
            string saveDir = Path.Combine(_appData.DataDirectory, "Clone Hero Imports");
            Directory.CreateDirectory(saveDir);
            string projectPath = Path.Combine(saveDir, $"{SanitizeFileName(songName)}.ghproj");
            data.projectPath = projectPath;
            File.WriteAllText(projectPath, data.ToJson());
        }
        else
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PenguinComb");
            Directory.CreateDirectory(tempDir);
            string projectPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.ghproj");
            data.projectPath = projectPath;
            File.WriteAllText(projectPath, data.ToJson());
        }

        return data;
    }

    private static SongProjectState CreateState(SongProjectData data)
    {
        string game = data.gameSelect is "GH3" or "GHA" or "GHWT" or "GH5" or "GHWoR" ? data.gameSelect : "GH3";
        string platform = data.platformSelect is "PC" or "PS2" or "Xbox 360" or "PS3" ? data.platformSelect : "PC";

        return new SongProjectState
        {
            Data = data,
            CurrentGame = game,
            CurrentPlatform = platform,
            EffectiveSongName = data.songName,
            PreviewStartTime = data.previewStart,
            PreviewEndTime = data.previewEnd
        };
    }

    private static string DisplayName(string path)
    {
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    /// <summary>Appends a source tag with a clean separator: "My Song" + "GH 2"
    /// becomes "My Song - GH 2" (a leading dash in the suffix avoids a double
    /// separator).</summary>
    private static string ApplyNameSuffix(string name, string suffix)
    {
        string trimmed = suffix.Trim();
        string separator = trimmed.StartsWith("-") ? " " : " - ";
        return name + separator + trimmed;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
