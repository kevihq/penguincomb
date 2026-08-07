namespace Honeycomb.Application.Abstractions;

/// <summary>Result of locating an external tool.</summary>
public sealed record ToolLocation(string ExecutablePath, string? Folder)
{
    public string EffectiveExecutable => ExecutablePath;
}

/// <summary>
/// Locates optional external tools (Onyx CLI, FFmpeg). Locations may be configured in
/// settings, discovered automatically, or chosen by the user.
/// </summary>
public interface IExternalToolLocator
{
    /// <summary>
    /// Returns the full path to the Onyx executable, or null if it cannot be found.
    /// The user is prompted to browse when <paramref name="browseIfMissing"/> is true.
    /// </summary>
    Task<string?> LocateOnyxAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the folder containing ffmpeg/ffprobe (or null to use PATH), prompting
    /// the user to browse when <paramref name="browseIfMissing"/> is true.
    /// </summary>
    Task<string?> LocateFfmpegAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default);

    /// <summary>True when the configured/required external tools are present.</summary>
    Task<ToolAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}

/// <summary>Which optional tools are present and usable.</summary>
public sealed record ToolAvailability(bool FfmpegFound, bool FfprobeFound, bool OnyxFound);
