using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.App.ViewModels;

/// <summary>A single song queued for batch compilation (.ghproj project or Clone Hero folder).</summary>
public partial class BatchSongItem : ObservableObject
{
    public BatchSongItem(string path, bool isCloneHero)
    {
        FilePath = path;
        IsCloneHero = isCloneHero;
        _songName = isCloneHero
            ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : Path.GetFileNameWithoutExtension(path);
    }

    public string FilePath { get; }
    public bool IsCloneHero { get; }

    /// <summary>"Clone Hero" or ".ghproj", shown next to the song name.</summary>
    public string KindText => IsCloneHero ? "Clone Hero" : ".ghproj";

    [ObservableProperty]
    private string _songName;

    /// <summary>Queued, Loading, Importing, Compiling, Done, Failed, Cancelled or Skipped.</summary>
    [ObservableProperty]
    private string _status = "Queued";

    [ObservableProperty]
    private string? _error;
}

/// <summary>
/// Compiles multiple songs in sequence. Each entry is either a .ghproj project or
/// a Clone Hero song folder (imported as a GH3 PC project first). Failures are
/// collected per song and shown in the list plus a summary at the end.
/// </summary>
public partial class BatchCompileViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly BatchCompileService _service;
    private CancellationTokenSource? _cts;

    public ObservableCollection<BatchSongItem> Songs { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMaximum = 1;

    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Optional text appended to the end of imported Clone Hero song names, e.g. "GH 2".</summary>
    [ObservableProperty]
    private string _nameSuffix = "";

    [ObservableProperty]
    private BatchSongItem? _selectedSong;

    public BatchCompileViewModel(IFileDialogService dialogs, IUserNotificationService notifications, BatchCompileService service)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _service = service;
    }

    [RelayCommand]
    private async Task AddFilesAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        var files = await _dialogs.PickOpenFilesAsync(new FileDialogOptions
        {
            Title = "Select .ghproj files to compile",
            Filters = [new FileFilter("GHProj files", "*.ghproj"), new FileFilter("All files", "*.*")],
            AllowMultiple = true
        }, cancellationToken);

        foreach (string file in files)
        {
            AddSource(file, isCloneHero: false);
        }
    }

    /// <summary>Picks multiple Clone Hero song folders at once.</summary>
    [RelayCommand]
    private async Task AddChSongsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        var folders = await _dialogs.PickFoldersAsync("Select Clone Hero song folders", cancellationToken: cancellationToken);

        int added = 0;
        foreach (string folder in folders)
        {
            if (AddSource(folder, isCloneHero: true))
            {
                added++;
            }
        }

        if (added == 0 && folders.Count > 0)
        {
            await _notifications.ShowMessageAsync("No Songs Added",
                "The selected folders were already in the list.", cancellationToken);
        }
    }

    [RelayCommand]
    private async Task AddChLibraryAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        string? root = await _dialogs.PickFolderAsync("Select the folder that contains your Clone Hero songs", cancellationToken: cancellationToken);
        if (root is null)
        {
            return;
        }

        int added = 0;
        foreach (string songFolder in FindChSongFolders(root))
        {
            if (AddSource(songFolder, isCloneHero: true))
            {
                added++;
            }
        }

        if (added == 0)
        {
            await _notifications.ShowMessageAsync("No Songs Found",
                "No Clone Hero song folders (folders containing a song.ini) were found in the selected folder.", cancellationToken);
        }
    }

    /// <summary>Finds every folder containing a song.ini, one level deep under the root.</summary>
    private static IEnumerable<string> FindChSongFolders(string root)
    {
        if (File.Exists(Path.Combine(root, "song.ini")))
        {
            yield return root;
        }

        foreach (string sub in Directory.EnumerateDirectories(root))
        {
            if (File.Exists(Path.Combine(sub, "song.ini")))
            {
                yield return sub;
            }
        }
    }

    private bool AddSource(string path, bool isCloneHero)
    {
        if (Songs.Any(s => s.IsCloneHero == isCloneHero && string.Equals(s.FilePath, path, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        Songs.Add(new BatchSongItem(path, isCloneHero));
        return true;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (IsBusy || SelectedSong is null)
        {
            return;
        }
        Songs.Remove(SelectedSong);
    }

    [RelayCommand]
    private void Clear()
    {
        if (IsBusy)
        {
            return;
        }
        Songs.Clear();
        ProgressValue = 0;
        StatusText = "";
    }

    [RelayCommand]
    private async Task CompileAllAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }
        if (Songs.Count == 0)
        {
            await _notifications.ShowMessageAsync("No Songs",
                "Add at least one .ghproj project or Clone Hero song before compiling.", cancellationToken);
            return;
        }

        IsBusy = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ProgressMaximum = Songs.Count;
        ProgressValue = 0;
        StatusText = "Starting batch compile...";
        try
        {
            var sources = Songs
                .Select(s => new BatchSource(s.IsCloneHero ? BatchSourceKind.CloneHeroFolder : BatchSourceKind.Project, s.FilePath))
                .ToList();
            foreach (var song in Songs)
            {
                song.Status = "Queued";
                song.Error = null;
            }

            var progress = new Progress<BatchCompileUpdate>(ApplyUpdate);
            string? suffix = string.IsNullOrWhiteSpace(NameSuffix) ? null : NameSuffix;
            var results = await _service.CompileAsync(sources, progress, _cts.Token, suffix);

            ApplyResults(results);

            int succeeded = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success && !r.Cancelled);
            string summary = failed == 0
                ? $"Batch compile finished: {succeeded} of {results.Count} song(s) compiled successfully."
                : $"Batch compile finished: {succeeded} succeeded, {failed} failed.";
            if (results.Count < sources.Count)
            {
                summary += " The remaining songs were skipped.";
            }

            StatusText = summary;
            await _notifications.ShowMessageAsync("Batch Compile Finished", summary, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Batch compile cancelled.";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void ApplyUpdate(BatchCompileUpdate update)
    {
        if (update.Completed < Songs.Count)
        {
            var item = Songs[update.Completed];
            item.SongName = update.CurrentSong;
            item.Status = update.Status;
        }
        ProgressValue = Math.Min(update.Completed, ProgressMaximum);
        StatusText = $"[{update.Completed}/{update.Total}] {update.CurrentSong} - {update.Status}";
    }

    private void ApplyResults(IReadOnlyList<BatchSongResult> results)
    {
        for (int i = 0; i < results.Count && i < Songs.Count; i++)
        {
            var result = results[i];
            var item = Songs[i];
            item.SongName = result.SongName;
            item.Status = result.Cancelled ? "Cancelled" : result.Success ? "Done" : "Failed";
            item.Error = result.Error;
        }

        // Songs never reached (the batch stopped early, e.g. cancelled).
        for (int i = results.Count; i < Songs.Count; i++)
        {
            Songs[i].Status = "Skipped";
        }

        ProgressValue = results.Count;
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
