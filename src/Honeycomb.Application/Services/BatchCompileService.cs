using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;

namespace Honeycomb.Application.Services;

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

    public BatchCompileService(ProjectFileService projects, SongCompileService compile, IAppDataLocator appData)
    {
        _projects = projects;
        _compile = compile;
        _appData = appData;
    }

    /// <summary>Compiles .ghproj projects (kept for the file-based batch flow).</summary>
    public Task<IReadOnlyList<BatchSongResult>> CompileAllAsync(
        IReadOnlyList<string> projectPaths,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
        => CompileAsync(projectPaths.Select(p => new BatchSource(BatchSourceKind.Project, p)).ToList(),
            progress, cancellationToken);

    /// <summary>Imports and compiles Clone Hero song folders as GH3 PC songs.</summary>
    public Task<IReadOnlyList<BatchSongResult>> CompileChFoldersAsync(
        IReadOnlyList<string> folders,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
        => CompileAsync(folders.Select(f => new BatchSource(BatchSourceKind.CloneHeroFolder, f)).ToList(),
            progress, cancellationToken);

    /// <summary>
    /// Compiles every source in order. Cancellation stops the batch after the
    /// current song; already-processed songs keep their results.
    /// </summary>
    public async Task<IReadOnlyList<BatchSongResult>> CompileAsync(
        IReadOnlyList<BatchSource> sources,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
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
                    data = CreateChProject(source.Path, out songName);
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
    /// chart + audio). The project is recorded as a .ghproj in the per-user data
    /// directory so the compile pipeline's auto-save writes there instead of
    /// prompting, and the imported song can be reopened and tweaked later.
    /// </summary>
    private SongProjectData CreateChProject(string folder, out string songName)
    {
        var data = _projects.LoadProjectSync(_projects.DefaultTemplatePath) ?? new SongProjectData();
        _projects.LoadFromChFolder(data, folder);

        // Target GH3 PC: this is the Clone Hero -> GH3 conversion flow.
        data.gameSelect = "GH3";
        data.platformSelect = "PC";

        string folderName = DisplayName(folder);
        songName = string.IsNullOrWhiteSpace(data.songName) ? folderName : data.songName;
        data.songName = songName;

        string saveDir = Path.Combine(_appData.DataDirectory, "Clone Hero Imports");
        Directory.CreateDirectory(saveDir);
        string projectPath = Path.Combine(saveDir, $"{SanitizeFileName(songName)}.ghproj");
        data.projectPath = projectPath;
        File.WriteAllText(projectPath, data.ToJson());

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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }
}
