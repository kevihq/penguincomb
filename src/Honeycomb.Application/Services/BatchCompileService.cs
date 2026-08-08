using Honeycomb.Application.Models;

namespace Honeycomb.Application.Services;

/// <summary>Outcome of compiling a single project in a batch run.</summary>
public sealed record BatchSongResult(string ProjectPath, string SongName, bool Success, string? Error, bool Cancelled);

/// <summary>Progress reported between songs during a batch run.</summary>
public sealed record BatchCompileUpdate(int Completed, int Total, string CurrentSong, string Status);

/// <summary>
/// Compiles a list of .ghproj projects sequentially, one song at a time, through the
/// exact same pipeline as the single-song compiler. A failure in one song does not
/// stop the batch; every project gets a result entry and errors are reported in the
/// result list instead of modal dialogs.
/// </summary>
public class BatchCompileService
{
    private readonly ProjectFileService _projects;
    private readonly SongCompileService _compile;

    public BatchCompileService(ProjectFileService projects, SongCompileService compile)
    {
        _projects = projects;
        _compile = compile;
    }

    /// <summary>
    /// Compiles every project in order. Cancellation stops the batch after the
    /// current song; already-processed songs keep their results.
    /// </summary>
    public async Task<IReadOnlyList<BatchSongResult>> CompileAllAsync(
        IReadOnlyList<string> projectPaths,
        IProgress<BatchCompileUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BatchSongResult>(projectPaths.Count);
        int completed = 0;

        foreach (string path in projectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string songName = Path.GetFileNameWithoutExtension(path);
            progress?.Report(new BatchCompileUpdate(completed, projectPaths.Count, songName, "Loading project..."));

            try
            {
                var data = await _projects.LoadProjectAsync(path, cancellationToken);
                if (data is null)
                {
                    results.Add(new BatchSongResult(path, songName, false, "Could not read the project file.", false));
                    completed++;
                    continue;
                }

                songName = data.songName;
                var state = CreateState(data);
                progress?.Report(new BatchCompileUpdate(completed, projectPaths.Count, songName, "Compiling..."));

                SongCompileResult result;
                _projects.SetAllToAbsolute(data);
                try
                {
                    // suppressMessages: batch failures are reported per song in the
                    // result list and the summary, not as interrupting modal dialogs.
                    result = await _compile.CompileAllAsync(state,
                        new CompileOptions { ShowPostCompile = false },
                        progress: null,
                        cancellationToken,
                        suppressMessages: true);
                }
                finally
                {
                    _projects.SetAllToRelative(data);
                }

                if (result.Cancelled)
                {
                    results.Add(new BatchSongResult(path, songName, false, "Cancelled", true));
                    return results;
                }

                results.Add(new BatchSongResult(path, songName, result.Success,
                    result.Error?.Message ?? (result.Success ? null : "Compilation failed."), false));
            }
            catch (OperationCanceledException)
            {
                results.Add(new BatchSongResult(path, songName, false, "Cancelled", true));
                return results;
            }
            catch (Exception ex)
            {
                results.Add(new BatchSongResult(path, songName, false, ex.Message, false));
            }

            completed++;
        }

        return results;
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
}
