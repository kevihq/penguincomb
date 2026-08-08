namespace Honeycomb.Application.Abstractions;

/// <summary>A file-type filter entry for native pickers ("Audio files", "*.mp3;*.ogg").</summary>
public sealed record FileFilter(string DisplayName, string Patterns);

public sealed record FileDialogOptions
{
    public string Title { get; init; } = "";
    public IReadOnlyList<FileFilter> Filters { get; init; } = Array.Empty<FileFilter>();
    public string? InitialDirectory { get; init; }
    public string? SuggestedFileName { get; init; }
    public bool AllowMultiple { get; init; }
}

/// <summary>
/// Native file/folder pickers. Implemented by the UI layer (Avalonia) and faked in tests.
/// All methods return null when the user cancels.
/// </summary>
public interface IFileDialogService
{
    Task<string?> PickOpenFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> PickOpenFilesAsync(FileDialogOptions options, CancellationToken cancellationToken = default);

    Task<string?> PickSaveFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>Picks multiple folders at once (empty list when the user cancels).</summary>
    Task<IReadOnlyList<string>> PickFoldersAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default);
}

public static class FileDialogOptionsExtensions
{
    public static string ToFilterString(this FileDialogOptions options) =>
        string.Join("|", options.Filters.Select(f => $"{f.DisplayName}|{f.Patterns}"));
}
