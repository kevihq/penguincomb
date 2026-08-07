using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using Honeycomb.Application.Services;

namespace Honeycomb.App.ViewModels;

public partial class SongListManagerViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly SongListService _service;
    private readonly SghImportService _sgh;

    public ObservableCollection<SghSongItem> Songs { get; } = new();
    public SongListState State { get; } = new();

    public IReadOnlyList<string> ConsoleOptions { get; } = ["PC", "360", "PS3"];

    [ObservableProperty]
    private bool _isGh3 = true;

    [ObservableProperty]
    private int _consoleIndex;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _sghPath = "";

    // Option enablement mirrors the legacy tab/console gating
    [ObservableProperty]
    private bool _importEnabled = true;

    [ObservableProperty]
    private bool _exportEnabled = true;

    [ObservableProperty]
    private bool _deleteEnabled = true;

    [ObservableProperty]
    private bool _gameSelectionEnabled = true;

    partial void OnIsGh3Changed(bool value) => UpdateOptions();
    partial void OnConsoleIndexChanged(int value) => UpdateOptions();
    partial void OnSelectedTabIndexChanged(int value)
    {
        // Legacy behavior: switching tabs clears the loaded list
        Songs.Clear();
        UpdateOptions();
    }

    private void UpdateOptions()
    {
        switch (ConsoleIndex)
        {
            case 0: // PC
                ImportEnabled = true;
                ExportEnabled = true;
                DeleteEnabled = true;
                GameSelectionEnabled = true;
                break;
            default: // 360 / PS3
                ImportEnabled = true;
                ExportEnabled = false;
                DeleteEnabled = false;
                GameSelectionEnabled = false;
                IsGh3 = true;
                break;
        }

        if (!IsGh3 && ConsoleIndex == 0)
        {
            // GHA only supports the Delete tab (legacy behavior)
            SelectedTabIndex = 2;
            DeleteEnabled = true;
            ImportEnabled = false;
            ExportEnabled = false;
        }
    }

    public SongListManagerViewModel(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        SongListService service,
        SghImportService sgh)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _service = service;
        _sgh = sgh;
    }

    private string CurrentGame => IsGh3 ? GameNames.GH3 : GameNames.GHA;

    [RelayCommand]
    private async Task LoadSetlistAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var songs = await _service.LoadSetlistAsync(State, CurrentGame, cancellationToken);
            Songs.Clear();
            foreach (var song in songs)
            {
                Songs.Add(new SghSongItem(new SghSongEntry(song.Split(' ')[0], "", "", null!)));
            }
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load setlist: {ex}");
            await _notifications.ShowErrorAsync("Load Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportSghFileAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Select SGH File",
            Filters = [new FileFilter("SGH Files", "*.sgh"), new FileFilter("Zip Files", "*.zip"), new FileFilter("All files", "*.*")]
        }, cancellationToken);
        if (path is null)
        {
            return;
        }

        try
        {
            var result = _sgh.LoadSGH(path);
            SghPath = path;
            Songs.Clear();
            foreach (var song in result.Songs)
            {
                Songs.Add(new SghSongItem(song));
            }
        }
        catch (Exception ex)
        {
            await _notifications.ShowErrorAsync("Import Failed", ex.Message, cancellationToken);
        }
    }

    [RelayCommand]
    private async Task ConvertSongsAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var checkedSongs = Songs.Where(s => s.IsChecked).Select(s => s.DisplayName).ToList();
            await _service.ConvertSongsAsync(SghPath, checkedSongs, ConsoleOptions[ConsoleIndex], null, cancellationToken);
            Songs.Clear();
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SGH conversion failed: {ex}");
            await _notifications.ShowErrorAsync("Conversion Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    private void SetAll(bool value)
    {
        foreach (var song in Songs)
        {
            song.IsChecked = value;
        }
    }

    [RelayCommand]
    private async Task ExportToSghAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var checkedSongs = Songs.Where(s => s.IsChecked).Select(s => s.DisplayName).ToList();
            await _service.ExportSongsAsync(State, checkedSongs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SGH export failed: {ex}");
            await _notifications.ShowErrorAsync("Export Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var checkedSongs = Songs.Where(s => s.IsChecked).Select(s => s.DisplayName).ToList();
            await _service.DeleteSongsAsync(State, checkedSongs, cancellationToken);
            Songs.Clear();
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Song deletion failed: {ex}");
            await _notifications.ShowErrorAsync("Delete Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreSetlistAsync(CancellationToken cancellationToken)
    {
        await _service.RestoreSetlistAsync(CurrentGame, cancellationToken);
    }
}
