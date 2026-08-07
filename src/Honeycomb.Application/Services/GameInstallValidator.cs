using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;

namespace Honeycomb.Application.Services;

/// <summary>
/// Validates Guitar Hero installation folders. A folder is considered a valid game
/// install when the Windows executable is present (also accepted inside Wine/Proton
/// prefixes on Linux) OR the expected DATA/PAK/MUSIC/SONGS layout exists with the
/// QB PAK file. On Linux the executable alone is NOT sufficient - the data folders
/// must also exist.
/// </summary>
public class GameInstallValidator
{
    private readonly IPlatformService _platform;

    public GameInstallValidator(IPlatformService platform)
    {
        _platform = platform;
    }

    public static bool IsKnownGame(string game) => game == GameNames.GH3 || game == GameNames.GHA;

    /// <summary>
    /// Full validation. Returns detailed findings; <see cref="GameInstallInfo.IsValid"/>
    /// requires the data layout (on Windows the presence of the executable is accepted
    /// as the primary indicator, since the data layout is re-validated by compile steps).
    /// </summary>
    public GameInstallInfo Validate(string folder, string game)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new GameInstallInfo
            {
                Game = game,
                FolderPath = folder,
                MissingItems = ["Folder does not exist"]
            };
        }

        var missing = new List<string>();

        string exePath = Path.Combine(folder, GameConstants.GetExeName(game));
        bool exeFound = File.Exists(exePath);

        bool dataFound = Directory.Exists(Path.Combine(folder, GameConstants.DATA));
        bool pakFound = Directory.Exists(Path.Combine(folder, GameConstants.DATA, GameConstants.PAK));
        bool qbPakFound = File.Exists(Path.Combine(folder, GameConstants.DATA, GameConstants.PAK, GameConstants.QbPakFilename));
        bool musicFound = Directory.Exists(Path.Combine(folder, GameConstants.DATA, GameConstants.MUSIC));
        bool songsFound = Directory.Exists(Path.Combine(folder, GameConstants.DATA, GameConstants.SONGS));

        if (!dataFound) missing.Add($"{GameConstants.DATA}");
        if (!pakFound) missing.Add($"{GameConstants.DATA}/{GameConstants.PAK}");
        if (!qbPakFound) missing.Add($"{GameConstants.DATA}/{GameConstants.PAK}/{GameConstants.QbPakFilename}");
        if (!musicFound) missing.Add($"{GameConstants.DATA}/{GameConstants.MUSIC}");
        if (!songsFound) missing.Add($"{GameConstants.DATA}/{GameConstants.SONGS}");

        if (game == GameNames.GH3)
        {
            string patchPath = Path.Combine(folder, GameConstants.DATA, GameConstants.PatchPakFilename);
            if (!File.Exists(patchPath))
            {
                missing.Add($"{GameConstants.DATA}/{GameConstants.PatchPakFilename} (required by BetterGH3)");
            }
        }

        bool valid = dataFound && pakFound && qbPakFound;
        if (_platform.IsWindows && exeFound)
        {
            valid = true;
        }

        return new GameInstallInfo
        {
            Game = game,
            FolderPath = folder,
            ExecutableFound = exeFound,
            DataFolderFound = dataFound,
            PakFolderFound = pakFound,
            QbPakFound = qbPakFound,
            MusicFolderFound = musicFound,
            SongsFolderFound = songsFound,
            IsValid = valid,
            MissingItems = missing
        };
    }
}
