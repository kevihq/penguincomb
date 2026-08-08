using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;
using Microsoft.Win32;

namespace PenguinComb.Infrastructure.GameLocators;

/// <summary>
/// Windows game-install discovery. Tries the Windows registry (best-effort key list)
/// then falls back to common install locations and manual user selection.
/// All registry access is guarded by a runtime Windows check and lives only here.
/// </summary>
public class WindowsGameInstallLocator : GameInstallLocatorBase
{
    // Best-effort registry locations for the Activision/Aspyr PC ports. The exact
    // keys differ between installers, so every candidate value is validated against
    // the actual game folder layout before being accepted.
    private static readonly string[] RegistryKeys =
    [
        @"SOFTWARE\Activision\Guitar Hero III",
        @"SOFTWARE\WOW6432Node\Activision\Guitar Hero III",
        @"SOFTWARE\Aspyr\Guitar Hero III",
        @"SOFTWARE\WOW6432Node\Aspyr\Guitar Hero III",
        @"SOFTWARE\Activision\Guitar Hero Aerosmith",
        @"SOFTWARE\WOW6432Node\Activision\Guitar Hero Aerosmith",
        @"SOFTWARE\Aspyr\Guitar Hero Aerosmith",
        @"SOFTWARE\WOW6432Node\Aspyr\Guitar Hero Aerosmith",
    ];

    private static readonly string[] ValueNames = ["InstallDir", "InstallPath", "Path", "GamePath", "Folder", "Default"];

    public WindowsGameInstallLocator(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        GameInstallValidator validator,
        IPlatformService platform)
        : base(dialogs, notifications, validator, platform)
    {
    }

    public override Task<string?> TryFindExistingAsync(string game, CancellationToken cancellationToken = default)
    {
        var candidates = new List<string>();

        if (Platform.IsWindows)
        {
            string? fromRegistry = FindInRegistry(game);
            if (fromRegistry != null)
            {
                candidates.Add(fromRegistry);
            }
        }

        // Common default install locations
        string programFiles = Platform.GetEnvironmentVariable("ProgramFiles(x86)", Platform.GetEnvironmentVariable("ProgramFiles", ""));
        if (!string.IsNullOrEmpty(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "Activision", GameConstants.GetGameDisplayName(game)));
            candidates.Add(Path.Combine(programFiles, "Aspyr", GameConstants.GetGameDisplayName(game)));
            candidates.Add(Path.Combine(programFiles, GameConstants.GetGameDisplayName(game)));
        }

        return Task.FromResult(FirstValid(candidates, game));
    }

    private string? FindInRegistry(string game)
    {
        try
        {
            foreach (string keyPath in RegistryKeys)
            {
                if (!keyPath.Contains(GameConstants.GetGameDisplayName(game), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key == null)
                {
                    continue;
                }

                foreach (string valueName in ValueNames)
                {
                    object? value = key.GetValue(valueName);
                    if (value is string path && Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registry lookup failed: {ex.Message}");
        }
        return null;
    }
}
