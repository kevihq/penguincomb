namespace Honeycomb.Application.Abstractions;

/// <summary>
/// Clears read-only/permission restrictions on a game folder so compile steps can write.
/// On Windows this clears the read-only attribute (elevation may still be required for
/// protected folders); on Linux it adds owner write permission recursively.
/// </summary>
public interface IFilePermissionService
{
    Task<bool> TryMakeWritableRecursiveAsync(
        string folder,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
