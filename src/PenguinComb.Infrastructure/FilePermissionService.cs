using PenguinComb.Application.Abstractions;

namespace PenguinComb.Infrastructure;

/// <summary>
/// Removes read-only restrictions so compile steps can write into game folders.
/// On Linux this adds owner write permission recursively (no root required for the
/// user's own files); on Windows it clears the read-only attribute on files/directories.
/// </summary>
public class FilePermissionService : IFilePermissionService
{
    private readonly IPlatformService _platform;

    public FilePermissionService(IPlatformService platform)
    {
        _platform = platform;
    }

    public Task<bool> TryMakeWritableRecursiveAsync(string folder, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (_platform.IsLinux)
            {
                MakeWritableLinux(folder, progress, cancellationToken);
            }
            else
            {
                MakeWritableWindows(folder, progress, cancellationToken);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update permissions for {folder}: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    private void MakeWritableLinux(string folder, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        int count = 0;
        foreach (string dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddOwnerWrite(dir);
            if (++count % 50 == 0)
            {
                progress?.Report($"Updating permissions ({count} folders)...");
            }
        }
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddOwnerWrite(file);
        }
        AddOwnerWrite(folder);
    }

    private static void AddOwnerWrite(string path)
    {
        var mode = File.GetUnixFileMode(path);
        mode |= UnixFileMode.UserWrite | UnixFileMode.UserRead | UnixFileMode.UserExecute;
        File.SetUnixFileMode(path, mode);
    }

    private void MakeWritableWindows(string folder, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.SetAttributes(file, FileAttributes.Normal);
        }
        foreach (string dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.SetAttributes(dir, FileAttributes.Directory);
        }
    }
}
