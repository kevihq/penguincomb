using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.App.ViewModels;

public partial class WadToolsViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly ISettingsService _settings;
    private readonly WadToolService _service;

    public WadToolsViewModel(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        ISettingsService settings,
        WadToolService service)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _settings = settings;
        _service = service;
        RecompileQb = settings.Settings.RecompileQb;
    }

    [ObservableProperty]
    private string _wadFile = "";

    [ObservableProperty]
    private bool _isExtracting;

    [ObservableProperty]
    private string _wadFolderToCompile = "";

    [ObservableProperty]
    private bool _recompileQb;

    [ObservableProperty]
    private bool _isCompiling;

    partial void OnRecompileQbChanged(bool value)
    {
        _settings.Settings.RecompileQb = value;
        // The generated property setter cannot await; persist without blocking the UI thread.
        _ = PersistSettingsAsync();
    }

    private async Task PersistSettingsAsync()
    {
        try
        {
            await _settings.SaveAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex}");
        }
    }

    [RelayCommand]
    private async Task OpenWadAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Select a WAD file",
            Filters = [new FileFilter("WAD files", "*.wad;*.hed"), new FileFilter("All files", "*.*")]
        }, cancellationToken);
        if (path is null)
        {
            return;
        }
        WadFile = File.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
    }

    [RelayCommand]
    private async Task ExtractAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(WadFile))
        {
            return;
        }
        IsExtracting = true;
        try
        {
            await _service.ExtractAsync(WadFile, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WAD extraction failed: {ex}");
            await _notifications.ShowErrorAsync("Extraction Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsExtracting = false;
        }
    }

    [RelayCommand]
    private async Task SelectFolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the extracted WAD folder", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            WadFolderToCompile = folder;
        }
    }

    [RelayCommand]
    private async Task CompileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(WadFolderToCompile))
        {
            return;
        }
        IsCompiling = true;
        try
        {
            await _service.CompileAsync(WadFolderToCompile, RecompileQb, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WAD compilation failed: {ex}");
            await _notifications.ShowErrorAsync("Compile Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsCompiling = false;
        }
    }
}
