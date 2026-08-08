using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.Infrastructure;

/// <summary>
/// Locates optional external tools. Onyx resolves to a full executable path
/// (onyx.exe on Windows, "onyx" on Linux - or any user-selected executable);
/// FFmpeg resolves to the folder containing ffmpeg/ffprobe (or PATH when unset).
/// </summary>
public class ExternalToolLocator : IExternalToolLocator
{
    private readonly ISettingsService _settings;
    private readonly IExternalProcessService _processes;
    private readonly IFileDialogService _dialogs;
    private readonly IUserNotificationService _notifications;
    private readonly IPlatformService _platform;

    public ExternalToolLocator(
        ISettingsService settings,
        IExternalProcessService processes,
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        IPlatformService platform)
    {
        _settings = settings;
        _processes = processes;
        _dialogs = dialogs;
        _notifications = notifications;
        _platform = platform;
    }

    public AppSettings Pref => _settings.Settings;

    public async Task<string?> LocateOnyxAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default)
    {
        // 1. Configured path (may be a full executable path or a containing folder)
        string configured = Pref.OnyxCliPath;
        if (ResolveOnyxExecutable(configured) is { } fromConfig)
        {
            return fromConfig;
        }

        // 2. On PATH
        string onyxName = _platform.IsWindows ? GameConstants.OnyxExeName : GameConstants.OnyxExeNameLinux;
        string? onPath = await _processes.FindExecutableOnPathAsync(onyxName, cancellationToken);
        if (onPath != null)
        {
            Pref.OnyxCliPath = onPath;
            await _settings.SaveAsync(cancellationToken);
            return onPath;
        }

        // 3. User selection
        if (!browseIfMissing)
        {
            return null;
        }

        await _notifications.ShowMessageAsync("Folder Required",
            "Onyx has not been found. Please select your Onyx CLI now.", cancellationToken);

        string? chosen = await _dialogs.PickOpenFileAsync(new FileDialogOptions
        {
            Title = "Select the Onyx CLI executable",
            Filters =
            [
                new FileFilter(_platform.IsWindows ? "Onyx executable" : "Onyx executable", _platform.IsWindows ? "onyx.exe" : "onyx"),
                new FileFilter("All files", "*.*")
            ]
        }, cancellationToken);

        if (chosen is null)
        {
            return null;
        }

        Pref.OnyxCliPath = chosen;
        await _settings.SaveAsync(cancellationToken);
        return chosen;
    }

    /// <summary>Accepts a full executable path or a containing folder.</summary>
    private string? ResolveOnyxExecutable(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        if (File.Exists(path))
        {
            return path;
        }
        if (Directory.Exists(path))
        {
            string name = _platform.IsWindows ? GameConstants.OnyxExeName : GameConstants.OnyxExeNameLinux;
            string candidate = Path.Combine(path, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    public async Task<string?> LocateFfmpegAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default)
    {
        // 1. Configured folder
        string configured = Pref.FfmpegPath;
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured) &&
            (File.Exists(Path.Combine(configured, "ffmpeg")) || File.Exists(Path.Combine(configured, "ffmpeg.exe"))))
        {
            ApplyFfmpegFolder(configured);
            return configured;
        }

        // 2. On PATH
        string? ffmpeg = await _processes.FindExecutableOnPathAsync("ffmpeg", cancellationToken);
        if (ffmpeg != null)
        {
            ApplyFfmpegFolder(null);
            return null; // PATH is used; nothing to store
        }

        // 3. User selection
        if (!browseIfMissing)
        {
            return null;
        }

        string? folder = await _dialogs.PickFolderAsync("Select the folder containing ffmpeg and ffprobe", cancellationToken: cancellationToken);
        if (folder is null)
        {
            return null;
        }
        if (!File.Exists(Path.Combine(folder, "ffmpeg")) && !File.Exists(Path.Combine(folder, "ffmpeg.exe")))
        {
            await _notifications.ShowErrorAsync("File Not Found",
                "ffmpeg was not found in the selected folder. Please select the folder containing the ffmpeg and ffprobe binaries.", cancellationToken);
            return null;
        }

        Pref.FfmpegPath = folder;
        await _settings.SaveAsync(cancellationToken);
        ApplyFfmpegFolder(folder);
        return folder;
    }

    private static void ApplyFfmpegFolder(string? folder)
    {
        GH_Toolkit_Core.Methods.GlobalVariables.ConfigureFFmpeg(folder);
    }

    public async Task<ToolAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        bool ffmpeg = await _processes.FindExecutableOnPathAsync("ffmpeg", cancellationToken) != null;
        bool ffprobe = await _processes.FindExecutableOnPathAsync("ffprobe", cancellationToken) != null;

        string configuredFfmpeg = Pref.FfmpegPath;
        if (!string.IsNullOrEmpty(configuredFfmpeg) && Directory.Exists(configuredFfmpeg))
        {
            ffmpeg |= File.Exists(Path.Combine(configuredFfmpeg, "ffmpeg")) || File.Exists(Path.Combine(configuredFfmpeg, "ffmpeg.exe"));
            ffprobe |= File.Exists(Path.Combine(configuredFfmpeg, "ffprobe")) || File.Exists(Path.Combine(configuredFfmpeg, "ffprobe.exe"));
        }

        bool onyx = ResolveOnyxExecutable(Pref.OnyxCliPath) != null ||
                    await _processes.FindExecutableOnPathAsync(
                        _platform.IsWindows ? GameConstants.OnyxExeName : GameConstants.OnyxExeNameLinux,
                        cancellationToken) != null;

        return new ToolAvailability(ffmpeg, ffprobe, onyx);
    }
}
