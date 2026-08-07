using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;

namespace Honeycomb.App.ViewModels;

public partial class PakToolsViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly PakToolService _service;

    public PakToolsViewModel(IFileDialogService dialogs, IUserNotificationService notifications, PakToolService service)
    {
        _dialogs = dialogs;
        _notifications = notifications;
        _service = service;
    }

    // ---- Extract tab ----
    [ObservableProperty]
    private string _pakFileOrFolder = "";

    [ObservableProperty]
    private bool _convertQ;

    [ObservableProperty]
    private bool _isExtracting;

    // ---- Compile tab ----
    [ObservableProperty]
    private string _folderToCompile = "";

    [ObservableProperty]
    private string _saveLocation = "";

    [ObservableProperty]
    private int _consoleIndex; // 0 = 360/PC, 1 = PS3, 2 = PS2, 3 = Wii

    [ObservableProperty]
    private bool _splitPab;

    [ObservableProperty]
    private string _game = "Select Game";

    [ObservableProperty]
    private bool _setAssetContext;

    [ObservableProperty]
    private string _assetContext = "";

    [ObservableProperty]
    private bool _isCompiling;

    public IReadOnlyList<string> GameOptions { get; } = ["Select Game", "GH3", "GHWT", "GHWoR"];

    partial void OnFolderToCompileChanged(string value) => UpdateSaveLocation();
    partial void OnConsoleIndexChanged(int value) => UpdateSaveLocation();

    private void UpdateSaveLocation()
    {
        if (string.IsNullOrEmpty(FolderToCompile))
        {
            SaveLocation = "";
            return;
        }
        string extension = ConsoleIndex switch
        {
            1 => ".pak.ps3",
            2 => ".pak.ps2",
            3 => ".pak.ngc",
            _ => ".pak.xen"
        };
        SaveLocation = $"{FolderToCompile}{extension}";
    }

    [RelayCommand]
    private async Task OpenPakAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Select a PAK file or folder",
            Filters = [new FileFilter("All files", "*.*")]
        }, cancellationToken);
        if (path is null)
        {
            return;
        }

        PakFileOrFolder = File.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
    }

    [RelayCommand]
    private async Task ExtractAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(PakFileOrFolder))
        {
            return;
        }

        IsExtracting = true;
        try
        {
            await _service.ExtractAsync(PakFileOrFolder, ConvertQ, cancellationToken);
        }
        finally
        {
            IsExtracting = false;
        }
    }

    [RelayCommand]
    private async Task SelectFolderAsync(CancellationToken cancellationToken)
    {
        string? folder = await _dialogs.PickFolderAsync("Select the folder containing files to compile", cancellationToken: cancellationToken);
        if (folder is not null)
        {
            FolderToCompile = folder;
        }
    }

    [RelayCommand]
    private async Task CompileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(FolderToCompile) || !Directory.Exists(FolderToCompile))
        {
            await _notifications.ShowWarningAsync("No Folder", "Please select a folder containing files to compile.");
            return;
        }
        if (!Game.StartsWith("GH"))
        {
            await _notifications.ShowWarningAsync("Select Game", "Please select a game.");
            return;
        }

        IsCompiling = true;
        try
        {
            await _service.CompileAsync(
                FolderToCompile,
                Game,
                (PakConsole)ConsoleIndex,
                SplitPab,
                SetAssetContext ? AssetContext : null,
                SaveLocation,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PAK compilation failed: {ex}");
            await _notifications.ShowErrorAsync("Compile Failed", ex.Message, cancellationToken);
        }
        finally
        {
            IsCompiling = false;
        }
    }
}
