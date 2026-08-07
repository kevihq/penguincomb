using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;

namespace Honeycomb.Infrastructure.GameLocators;

/// <summary>
/// Linux game-install discovery. Does not assume a native Linux executable: the game
/// is looked up inside Wine prefixes and Steam compatibility-data prefixes, and the
/// required data folders (DATA/PAK/MUSIC/SONGS + qb.pak.xen) are validated rather
/// than the presence of a Windows executable alone. Never blocks when discovery fails.
/// </summary>
public class LinuxGameInstallLocator : GameInstallLocatorBase
{
    public LinuxGameInstallLocator(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        GameInstallValidator validator,
        IPlatformService platform)
        : base(dialogs, notifications, validator, platform)
    {
    }

    public override Task<string?> TryFindExistingAsync(string game, CancellationToken cancellationToken = default)
    {
        if (!Platform.IsLinux)
        {
            return Task.FromResult<string?>(null);
        }

        var candidates = new List<string>();

        string home = Platform.GetEnvironmentVariable("HOME", "");
        string gameName = GameConstants.GetGameDisplayName(game);

        // Plain installs / mounted folders
        candidates.Add(Path.Combine(home, "Games", gameName));
        candidates.Add(Path.Combine(home, "Guitar Hero", gameName));
        candidates.Add($"/opt/{gameName}");
        candidates.Add($"/usr/local/games/{gameName}");
        candidates.Add($"/media/{Environment.UserName}/{gameName}");

        // Classic .wine prefix
        AddWinePrefixCandidates(candidates, Path.Combine(home, ".wine", "drive_c"), gameName);

        // Steam compatibility data (Proton prefixes) - native and legacy layouts
        AddSteamPrefixCandidates(candidates, Path.Combine(home, ".local", "share", "Steam"), gameName);
        AddSteamPrefixCandidates(candidates, Path.Combine(home, ".steam", "steam"), gameName);

        // Flatpak Steam
        AddSteamPrefixCandidates(candidates, Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "steam"), gameName);

        // Lutris prefixes
        AddWinePrefixCandidates(candidates, Path.Combine(home, ".local", "share", "lutris", "runners"), gameName);
        foreach (string prefix in SafeDirectories(Path.Combine(home, ".local", "share", "lutris", "prefixes")))
        {
            AddWinePrefixCandidates(candidates, Path.Combine(prefix, "drive_c"), gameName);
        }

        return Task.FromResult(FirstValid(candidates, game));
    }

    private void AddSteamPrefixCandidates(List<string> candidates, string steamRoot, string gameName)
    {
        foreach (string compatData in SafeDirectories(Path.Combine(steamRoot, "steamapps", "compatdata")))
        {
            string pfx = Path.Combine(compatData, "pfx", "drive_c");
            AddWinePrefixCandidates(candidates, pfx, gameName);
        }
    }

    private void AddWinePrefixCandidates(List<string> candidates, string driveC, string gameName)
    {
        if (!Directory.Exists(driveC))
        {
            return;
        }

        foreach (string programFiles in SafeDirectories(Path.Combine(driveC, "Program Files (x86)")))
        {
            candidates.Add(Path.Combine(programFiles, "Activision", gameName));
            candidates.Add(Path.Combine(programFiles, "Aspyr", gameName));
            candidates.Add(Path.Combine(programFiles, gameName));
        }
        foreach (string programFiles in SafeDirectories(Path.Combine(driveC, "Program Files")))
        {
            candidates.Add(Path.Combine(programFiles, "Activision", gameName));
            candidates.Add(Path.Combine(programFiles, "Aspyr", gameName));
            candidates.Add(Path.Combine(programFiles, gameName));
        }
    }

    private static IEnumerable<string> SafeDirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }
        try
        {
            return Directory.GetDirectories(path);
        }
        catch
        {
            return [];
        }
    }
}
