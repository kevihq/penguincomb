using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;
using GH_Toolkit_Core.PAK;

namespace Honeycomb.Application.Services;

/// <summary>
/// Port of the legacy <c>PreCompileChecks</c> static class: game-folder preflight,
/// BetterGH3 resource install, QB PAK backup, GHA replacement injection and Onyx
/// location. All user prompts go through the injected services instead of WinForms.
/// </summary>
public class PreCompileChecks
{
    private readonly ISettingsService _settings;
    private readonly IGameInstallLocator _gameLocator;
    private readonly IUserNotificationService _notifications;
    private readonly IExternalToolLocator _toolLocator;
    private readonly IAppDataLocator _appData;
    private readonly ResourceLocator _resources;
    private readonly GameInstallValidator _validator;
    private readonly IPlatformService _platform;

    public PreCompileChecks(
        ISettingsService settings,
        IGameInstallLocator gameLocator,
        IUserNotificationService notifications,
        IExternalToolLocator toolLocator,
        IAppDataLocator appData,
        ResourceLocator resources,
        GameInstallValidator validator,
        IPlatformService platform)
    {
        _settings = settings;
        _gameLocator = gameLocator;
        _notifications = notifications;
        _toolLocator = toolLocator;
        _appData = appData;
        _resources = resources;
        _validator = validator;
        _platform = platform;
    }

    public AppSettings Pref => _settings.Settings;

    public string GetGh3Folder(string game) =>
        game == GameNames.GH3 ? Pref.Gh3FolderPath : Pref.GhaFolderPath;

    public string GetGh3PakFile(string game) =>
        game == GameNames.GH3 ? Pref.Gh3QbPak : Pref.GhaQbPak;

    public string GetCustomsPak(string game) =>
        game == GameNames.GH3
            ? Path.Combine(GetGh3Folder(game), GameConstants.DATA, GameConstants.CustomsPakFilename)
            : GetGh3PakFile(game);

    /// <summary>
    /// Full PC preflight for a game: locate+validate the install, install BetterGH3
    /// resources (GH3), back up the QB PAK and inject GHA replacements when needed.
    /// </summary>
    public async Task Gh3PcCheckAsync(string game, bool suppressMessages = false, CancellationToken cancellationToken = default)
    {
        string backupLocation = Path.Combine(_appData.BackupsDirectory, game);
        string qbPakBackupLocation = Path.Combine(backupLocation, GameConstants.QB);
        string gameName = GameConstants.GetGameDisplayName(game);

        string ghPath = await ValidateAndGetGamePathAsync(game, cancellationToken);

        if (game == GameNames.GH3)
        {
            await CopyResourceFilesIfNeededAsync(ghPath, gameName, suppressMessages, cancellationToken);
        }

        string ghQbPakPath = Path.Combine(ghPath, GameConstants.DATA, GameConstants.PAK, GameConstants.QbPakFilename);
        string ghQbPabPath = Path.Combine(ghPath, GameConstants.DATA, GameConstants.PAK, GameConstants.QbPabFilename);

        bool backedUp = await BackupQbFilesIfNeededAsync(qbPakBackupLocation, ghQbPakPath, ghQbPabPath, backupLocation, gameName, cancellationToken);

        if (backedUp)
        {
            ReplaceGh3PakFiles(game, "PC");
        }
    }

    /// <summary>
    /// Validates the configured game path, prompting the user to browse until a valid
    /// folder is chosen. Throws <see cref="OperationCanceledException"/> when the user
    /// cancels. Updates settings when the path changes.
    /// </summary>
    public async Task<string> ValidateAndGetGamePathAsync(string game, CancellationToken cancellationToken = default)
    {
        string ghPath = GetGh3Folder(game);

        if (string.IsNullOrEmpty(ghPath))
        {
            ghPath = await _gameLocator.BrowseForGameFolderAsync(game, cancellationToken);
            await SaveGamePathAsync(game, ghPath, cancellationToken);
        }

        while (true)
        {
            var info = _validator.Validate(ghPath, game);
            if (info.IsValid)
            {
                break;
            }

            string message = $"The game installation was not found in the selected path. Please select the correct {GameConstants.GetGameDisplayName(game)} game folder.\n\n" +
                             $"Missing: {string.Join(", ", info.MissingItems)}";
            await _notifications.ShowWarningAsync("Game Not Found", message, cancellationToken);
            ghPath = await _gameLocator.BrowseForGameFolderAsync(game, cancellationToken);
            await SaveGamePathAsync(game, ghPath, cancellationToken);
        }

        return ghPath;
    }

    private async Task SaveGamePathAsync(string game, string ghPath, CancellationToken cancellationToken)
    {
        if (game == GameNames.GH3)
        {
            Pref.Gh3FolderPath = ghPath;
        }
        else
        {
            Pref.GhaFolderPath = ghPath;
        }
        await _settings.SaveAsync(cancellationToken);
    }

    /// <summary>Requires patch.pak.xen; installs the bundled BetterGH3 files when customs.pak.xen is missing.</summary>
    public async Task CopyResourceFilesIfNeededAsync(string ghPath, string gameName, bool forceCopy = false, CancellationToken cancellationToken = default)
    {
        string patchPakPath = Path.Combine(ghPath, GameConstants.DATA, GameConstants.PatchPakFilename);
        if (!File.Exists(patchPakPath))
        {
            throw new FileNotFoundException(
                $"Required file {GameConstants.PatchPakFilename} not found in {gameName}'s DATA folder.\n\nPlease re-download BetterGH3.");
        }

        string customsPakPath = Path.Combine(ghPath, GameConstants.DATA, GameConstants.CustomsPakFilename);
        if (File.Exists(customsPakPath) && !forceCopy)
        {
            return;
        }

        string betterGh3FilesPath = _resources.BetterGh3Path;
        if (!Directory.Exists(betterGh3FilesPath))
        {
            throw new FileNotFoundException("BetterGH3 update files not found. Please re-download Honeycomb.");
        }

        var betterGh3Files = Directory.GetFiles(betterGh3FilesPath, "*.*", SearchOption.AllDirectories);
        foreach (string file in betterGh3Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(betterGh3FilesPath, file);
            string destPath = Path.Combine(ghPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }

        if (!forceCopy)
        {
            await _notifications.ShowMessageAsync("BetterGH3 Updated",
                $"Better {gameName} has been updated to allow customs online!\n\nSave files will also no longer break when adding new songs.",
                cancellationToken);
        }
    }

    /// <summary>Backs up DATA/PAK/qb.pak.xen + qb.pab.xen into the per-user data directory once.</summary>
    public async Task<bool> BackupQbFilesIfNeededAsync(string qbPakBackupLocation, string ghQbPakPath, string ghQbPabPath, string backupLocation, string gameName, CancellationToken cancellationToken = default)
    {
        string pakBackup = qbPakBackupLocation + ".pak.xen";
        if (File.Exists(pakBackup))
        {
            return false;
        }

        Directory.CreateDirectory(backupLocation);

        try
        {
            File.Copy(ghQbPakPath, pakBackup);
            File.Copy(ghQbPabPath, qbPakBackupLocation + ".pab.xen");
            await _notifications.ShowMessageAsync("Backup Created",
                $"A backup of {gameName}'s QB file has been created.\nIt can be copied back to your GH folder at any time in the settings menu.",
                cancellationToken);
            return true;
        }
        catch (Exception)
        {
            await _notifications.ShowErrorAsync("Error",
                $"An error occurred while trying to backup {gameName}'s QB.PAK file.\n\nCancelling compilation.",
                cancellationToken);
            throw;
        }
    }

    /// <summary>Injects bundled replacement qb files into GHA's QB PAK (no-op for GH3).</summary>
    public void ReplaceGh3PakFiles(string game, string platform)
    {
        if (game == GameNames.GH3)
        {
            return;
        }

        string qbPakLocation = GetGh3PakFile(game);
        string replaceLocation = Path.Combine(_resources.ReplacementsPath, platform, game, GameConstants.QB);

        if (!Directory.Exists(replaceLocation))
        {
            return;
        }

        var pakCompiler = new PAK.PakCompiler(game, platform, split: true);
        var replaceFiles = Directory.GetFiles(replaceLocation, "*.qb", SearchOption.AllDirectories);
        var qbPak = PAK.PakEntryDictFromFile(qbPakLocation);

        foreach (var file in replaceFiles)
        {
            string relPath = Path.GetRelativePath(replaceLocation, file);
            if (qbPak.TryGetValue(relPath, out var entry))
            {
                byte[] qbData = File.ReadAllBytes(file);
                entry.OverwriteData(qbData);
            }
        }

        var (pakData, pabData) = pakCompiler.CompilePakFromDictionary(qbPak);
        OverwriteGh3Pak(pakData, pabData!, game);
    }

    public void OverwriteGh3Pak(byte[] pakData, byte[] pabData, string game)
    {
        string pak = GetCustomsPak(game);
        if (game == GameNames.GH3)
        {
            GH_Toolkit_Core.Methods.CreateForGame.OverwritePak(pak, pakData, ".xen");
        }
        else
        {
            GH_Toolkit_Core.Methods.CreateForGame.OverwriteSplitPak(pak, pakData, pabData, ".xen");
        }
    }

    /// <summary>
    /// Ensures the Onyx CLI executable is available, prompting the user to select it
    /// when missing. The full executable path is stored in settings.
    /// </summary>
    public async Task OnyxCheckAsync(CancellationToken cancellationToken = default)
    {
        string? onyxPath = await _toolLocator.LocateOnyxAsync(browseIfMissing: true, cancellationToken);
        if (onyxPath is null)
        {
            throw new OperationCanceledException("Onyx CLI location was not provided.");
        }

        if (Pref.OnyxCliPath != onyxPath)
        {
            Pref.OnyxCliPath = onyxPath;
            await _settings.SaveAsync(cancellationToken);
        }
    }

    /// <summary>Checks the GHWT MODS folder, prompting for it when unset.</summary>
    public async Task CheckForModsFolderAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Pref.WtModsFolder))
        {
            await _notifications.ShowMessageAsync("Folder Required",
                "Your GHWT mods folder has not been set. Please select your MODS folder now.",
                cancellationToken);
            string folder = await _gameLocator.BrowseForGameFolderAsync(GameNames.GHWT, cancellationToken);
            Pref.WtModsFolder = folder;
            await _settings.SaveAsync(cancellationToken);

            if (!Directory.Exists(Pref.WtModsFolder))
            {
                throw new Exception("GHWT Mods folder not set.");
            }
        }
    }
}
