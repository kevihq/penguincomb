using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Honeycomb.Application.Abstractions;

namespace Honeycomb.App.Services;

/// <summary>
/// Native file/folder pickers backed by Avalonia's StorageProvider (works on Linux,
/// Windows and macOS without Wine). On Linux the picker is provided by the desktop
/// portal service; when that service is missing or unresponsive the call is guarded
/// by a timeout so the application can never freeze on a dialog.
/// </summary>
public class AvaloniaFileDialogService : IFileDialogService
{
    private readonly Func<Window?> _windowProvider;
    private readonly IUserNotificationService _notifications;

    private static readonly TimeSpan PickerTimeout = TimeSpan.FromSeconds(45);

    public AvaloniaFileDialogService(Func<Window?> windowProvider, IUserNotificationService notifications)
    {
        _windowProvider = windowProvider;
        _notifications = notifications;
    }

    private IStorageProvider? GetStorage()
    {
        Window? window = _windowProvider();
        return window is null ? null : TopLevel.GetTopLevel(window)?.StorageProvider;
    }

    public async Task<string?> PickOpenFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return null;
        }

        try
        {
            var files = await WithTimeout(
                storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = options.Title,
                    AllowMultiple = false,
                    FileTypeFilter = ToFileTypes(options.Filters),
                    SuggestedStartLocation = ToFolder(options.InitialDirectory)
                }));

            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            await ReportPickerFailureAsync(ex, cancellationToken);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> PickOpenFilesAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return [];
        }

        try
        {
            var files = await WithTimeout(
                storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = options.Title,
                    AllowMultiple = true,
                    FileTypeFilter = ToFileTypes(options.Filters),
                    SuggestedStartLocation = ToFolder(options.InitialDirectory)
                }));

            return files.Select(f => f.TryGetLocalPath() ?? "").Where(p => p.Length > 0).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception ex)
        {
            await ReportPickerFailureAsync(ex, cancellationToken);
            return [];
        }
    }

    public async Task<string?> PickSaveFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return null;
        }

        try
        {
            var file = await WithTimeout(
                storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = options.Title,
                    SuggestedFileName = options.SuggestedFileName,
                    FileTypeChoices = ToFileTypes(options.Filters),
                    SuggestedStartLocation = ToFolder(options.InitialDirectory)
                }));

            return file?.TryGetLocalPath();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            await ReportPickerFailureAsync(ex, cancellationToken);
            return null;
        }
    }

    public async Task<string?> PickFolderAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return null;
        }

        try
        {
            var folders = await WithTimeout(
                storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    SuggestedStartLocation = ToFolder(initialDirectory)
                }));

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            await ReportPickerFailureAsync(ex, cancellationToken);
            return null;
        }
    }

    private async Task<T> WithTimeout<T>(Task<T> picker)
    {
        try
        {
            return await picker.WaitAsync(PickerTimeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                "The file picker did not respond. On Linux this usually means the desktop portal " +
                "(xdg-desktop-portal) service is not running or reachable. Please check your desktop " +
                "session and try again.");
        }
    }

    private Task ReportPickerFailureAsync(Exception ex, CancellationToken cancellationToken)
    {
        Console.WriteLine($"File picker failed: {ex.Message}");
        return _notifications.ShowErrorAsync("File Picker Unavailable",
            "The file/folder picker could not be opened.\n\n" + ex.Message, cancellationToken);
    }

    private static List<FilePickerFileType> ToFileTypes(IReadOnlyList<FileFilter> filters)
    {
        var result = new List<FilePickerFileType>();
        foreach (var filter in filters)
        {
            result.Add(new FilePickerFileType(filter.DisplayName)
            {
                Patterns = filter.Patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            });
        }
        return result;
    }

    private static IStorageFolder? ToFolder(string? path) => null;
}
