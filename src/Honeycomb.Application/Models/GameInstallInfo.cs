namespace Honeycomb.Application.Models;

/// <summary>Validation result for a game installation folder.</summary>
public sealed record GameInstallInfo
{
    public required string Game { get; init; }
    public required string FolderPath { get; init; }
    public bool ExecutableFound { get; init; }
    public bool DataFolderFound { get; init; }
    public bool PakFolderFound { get; init; }
    public bool QbPakFound { get; init; }
    public bool MusicFolderFound { get; init; }
    public bool SongsFolderFound { get; init; }
    public bool IsValid { get; init; }

    public IReadOnlyList<string> MissingItems { get; init; } = Array.Empty<string>();

    public override string ToString()
    {
        var missing = MissingItems.Count == 0 ? "none" : string.Join(", ", MissingItems);
        return $"{FolderPath} (missing: {missing})";
    }
}

/// <summary>The games the toolkit knows how to work with (QB constant values).</summary>
public static class GameNames
{
    public const string GH3 = "GH3";
    public const string GHA = "GHA";
    public const string GHWT = "GHWT";
    public const string GH5 = "GH5";
    public const string GHWOR = "GHWoR";
}

/// <summary>Console platform identifiers (QB constant values).</summary>
public static class ConsoleNames
{
    public const string PC = "PC";
    public const string PS2 = "PS2";
    public const string PS3 = "PS3";
    public const string Xbox360 = "Xbox 360";
    public const string Wii = "Wii";
}
