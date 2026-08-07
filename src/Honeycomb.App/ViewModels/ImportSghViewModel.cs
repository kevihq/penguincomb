using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;

namespace Honeycomb.App.ViewModels;

/// <summary>A selectable song loaded from an SGH archive.</summary>
public partial class SghSongItem : ObservableObject
{
    public SghSongItem(SghSongEntry entry)
    {
        Entry = entry;
    }

    public SghSongEntry Entry { get; }
    public string DisplayName => Entry.DisplayName;

    [ObservableProperty]
    private bool _isChecked = true;
}

public partial class ImportSghViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly SghImportService _service;

    public ObservableCollection<SghSongItem> Songs { get; } = new();

    public IReadOnlyList<string> ConsoleOptions { get; } = ["PC", "360", "PS3"];

    [ObservableProperty]
    private int _consoleIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _sghPath = "";

    public ImportSghViewModel(IFileDialogService dialogs, IUserNotificationService notifications, SghImportService service)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _service = service;
    }

    [RelayCommand]
    private async Task ImportSghAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Import SGH File",
            Filters = [new FileFilter("SGH Files", "*.sgh"), new FileFilter("Zip Files", "*.zip"), new FileFilter("All files", "*.*")]
        }, cancellationToken);

        if (path is null)
        {
            return;
        }

        LoadSgh(path);
    }

    public void LoadSgh(string path)
    {
        SghPath = path;
        try
        {
            var result = _service.LoadSGH(path);
            Songs.Clear();
            foreach (var song in result.Songs)
            {
                Songs.Add(new SghSongItem(song));
            }

            if (result.Duplicates.Count > 0)
            {
                _notifications.ShowMessageAsync("Duplicates Found!",
                    $"The following songs are duplicates and will not be imported:\n\n{string.Join("\n", result.Duplicates)}").Wait();
            }

            if (Songs.Count == 0)
            {
                _notifications.ShowMessageAsync("No Songs Found", "No songs were found in the selected SGH file.").Wait();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load SGH: {ex}");
            _notifications.ShowErrorAsync("Import Failed", $"Could not read the SGH file:\n\n{ex.Message}").Wait();
        }
    }

    [RelayCommand]
    private async Task ConvertAsync(CancellationToken cancellationToken)
    {
        if (Songs.Count == 0)
        {
            await _notifications.ShowMessageAsync("No Songs Loaded", "No songs loaded!\n\nPlease import an SGH file first.", cancellationToken);
            return;
        }

        IsBusy = true;
        try
        {
            var checkedSongs = Songs.Where(s => s.IsChecked).Select(s => s.DisplayName).ToList();
            if (checkedSongs.Count == 0)
            {
                await _notifications.ShowMessageAsync("No Songs Selected", "No songs selected to import.", cancellationToken);
                return;
            }
            await _service.ConvertSongsAsync(SghPath, checkedSongs, ConsoleOptions[ConsoleIndex], null, cancellationToken);
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
}
