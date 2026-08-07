using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Honeycomb.Application.Abstractions;

namespace Honeycomb.App.Services;

/// <summary>
/// Native file/folder pickers backed by Avalonia's StorageProvider (works on Linux,
/// Windows and macOS without Wine). Requires a window owner, provided by the app.
/// </summary>
public class AvaloniaFileDialogService : IFileDialogService
{
    private readonly Func<Window?> _windowProvider;

    public AvaloniaFileDialogService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider;
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

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = options.Title,
            AllowMultiple = false,
            FileTypeFilter = ToFileTypes(options.Filters),
            SuggestedStartLocation = ToFolder(options.InitialDirectory)
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<IReadOnlyList<string>> PickOpenFilesAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return [];
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = options.Title,
            AllowMultiple = true,
            FileTypeFilter = ToFileTypes(options.Filters),
            SuggestedStartLocation = ToFolder(options.InitialDirectory)
        });

        return files.Select(f => f.TryGetLocalPath() ?? "").Where(p => p.Length > 0).ToList();
    }

    public async Task<string?> PickSaveFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = options.Title,
            SuggestedFileName = options.SuggestedFileName,
            FileTypeChoices = ToFileTypes(options.Filters),
            SuggestedStartLocation = ToFolder(options.InitialDirectory)
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default)
    {
        var storage = GetStorage();
        if (storage is null)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = ToFolder(initialDirectory)
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
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
