using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;

namespace Honeycomb.App.ViewModels;

/// <summary>A single .ghproj queued for batch compilation.</summary>
public partial class BatchSongItem : ObservableObject
{
    public BatchSongItem(string path)
    {
        FilePath = path;
        _songName = System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public string FilePath { get; }

    [ObservableProperty]
    private string _songName;

    /// <summary>Queued, Loading, Compiling, Done, Failed, Cancelled or Skipped.</summary>
    [ObservableProperty]
    private string _status = "Queued";

    [ObservableProperty]
    private string? _error;
}

/// <summary>
/// Compiles multiple .ghproj projects in sequence. Each song runs through the same
/// pipeline as the single-song compiler; failures are collected per song and shown
/// in the list plus a summary at the end.
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
            AddProject(file);
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        string? folder = await _dialogs.PickFolderAsync("Select a folder containing .ghproj files", cancellationToken: cancellationToken);
        if (folder is null)
        {
            return;
        }

        int added = 0;
        foreach (string file in Directory.EnumerateFiles(folder, "*.ghproj", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (AddProject(file))
            {
                added++;
            }
        }

        if (added == 0)
        {
            await _notifications.ShowMessageAsync("No Projects Found",
                "No .ghproj files were found in the selected folder.", cancellationToken);
        }
    }

    private bool AddProject(string path)
    {
        if (Songs.Any(s => string.Equals(s.FilePath, path, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        Songs.Add(new BatchSongItem(path));
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
                "Add at least one .ghproj project before compiling.", cancellationToken);
            return;
        }

        IsBusy = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ProgressMaximum = Songs.Count;
        ProgressValue = 0;
        StatusText = "Starting batch compile...";
        try
        {
            var paths = Songs.Select(s => s.FilePath).ToList();
            foreach (var song in Songs)
            {
                song.Status = "Queued";
                song.Error = null;
            }

            var progress = new Progress<BatchCompileUpdate>(ApplyUpdate);
            var results = await _service.CompileAllAsync(paths, progress, _cts.Token);

            ApplyResults(results);

            int succeeded = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success && !r.Cancelled);
            string summary = failed == 0
                ? $"Batch compile finished: {succeeded} of {results.Count} song(s) compiled successfully."
                : $"Batch compile finished: {succeeded} succeeded, {failed} failed.";
            if (results.Count < paths.Count)
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
