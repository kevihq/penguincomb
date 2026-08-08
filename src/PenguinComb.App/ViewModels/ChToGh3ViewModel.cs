using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.App.ViewModels;

/// <summary>
/// Quick "Clone Hero to Better GH3" flow: pick Clone Hero song folders, point at
/// the GH3 game folder (remembered in settings), convert. No .ghproj project
/// files are created or saved - songs are compiled straight from their folders.
/// </summary>
public partial class ChToGh3ViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly BatchCompileService _service;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _cts;

    public ObservableCollection<BatchSongItem> Songs { get; } = new();

    [ObservableProperty]
    private string _gh3Folder = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMaximum = 1;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private BatchSongItem? _selectedSong;

    public ChToGh3ViewModel(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        BatchCompileService service,
        ISettingsService settings)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _service = service;
        _settings = settings;
        Gh3Folder = settings.Settings.Gh3FolderPath;
    }

    [RelayCommand]
    private async Task AddSongsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        var folders = await _dialogs.PickFoldersAsync("Select Clone Hero song folders", cancellationToken: cancellationToken);
        foreach (string folder in folders)
        {
            AddSource(folder);
        }
    }

    [RelayCommand]
    private async Task AddLibraryAsync(CancellationToken cancellationToken)
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
            if (AddSource(songFolder))
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

    [RelayCommand]
    private async Task BrowseGh3FolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        string? folder = await _dialogs.PickFolderAsync("Select your Guitar Hero 3 game folder", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            Gh3Folder = folder;
        }
    }

    private bool AddSource(string path)
    {
        if (Songs.Any(s => string.Equals(s.FilePath, path, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        Songs.Add(new BatchSongItem(path, isCloneHero: true));
        return true;
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
    private async Task CompileAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }
        if (Songs.Count == 0)
        {
            await _notifications.ShowMessageAsync("No Songs",
                "Add at least one Clone Hero song folder first.", cancellationToken);
            return;
        }
        if (string.IsNullOrWhiteSpace(Gh3Folder))
        {
            await _notifications.ShowMessageAsync("GH3 Folder Required",
                "Select your Guitar Hero 3 game folder first.", cancellationToken);
            return;
        }

        IsBusy = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ProgressMaximum = Songs.Count;
        ProgressValue = 0;
        StatusText = "Starting conversion...";
        try
        {
            var paths = Songs.Select(s => s.FilePath).ToList();
            foreach (var song in Songs)
            {
                song.Status = "Queued";
                song.Error = null;
            }

            var progress = new Progress<BatchCompileUpdate>(ApplyUpdate);
            var results = await _service.CompileChToGh3Async(paths, Gh3Folder.Trim(), null, progress, _cts.Token);

            ApplyResults(results);

            int succeeded = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success && !r.Cancelled);
            string summary = failed == 0
                ? $"Conversion finished: {succeeded} of {results.Count} song(s) added to the game."
                : $"Conversion finished: {succeeded} added, {failed} failed.";
            if (results.Count < paths.Count)
            {
                summary += " The remaining songs were skipped.";
            }

            StatusText = summary;
            await _notifications.ShowMessageAsync("Conversion Finished", summary, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = "Conversion failed.";
            await _notifications.ShowErrorAsync("Invalid GH3 Folder", ex.Message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Conversion cancelled.";
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
