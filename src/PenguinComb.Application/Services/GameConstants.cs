using PenguinComb.Application.Models;

namespace PenguinComb.Application.Services;

/// <summary>
/// Port of the legacy <c>PreCompileChecks</c> path/name constants. Game-folder layout
/// constants use relative names so they work under Wine/Proton prefixes and native
/// installs alike.
/// </summary>
public static class GameConstants
{
    // Game folder layout (relative to the game root)
    public const string DATA = "DATA";
    public const string PAK = "PAK";
    public const string MUSIC = "MUSIC";
    public const string SONGS = "SONGS";
    public const string QB = "QB";

    public const string CustomsPakFilename = "customs.pak.xen";
    public const string PatchPakFilename = "patch.pak.xen";
    public const string QbPakName = "qb";
    public const string QbPakFilename = "qb.pak.xen";
    public const string QbPabFilename = "qb.pab.xen";

    // Executable names (Windows-only game executables; accepted inside Wine/Proton prefixes on Linux)
    public const string Gh3ExeName = "GH3.exe";
    public const string GhaExeName = "Guitar Hero Aerosmith.exe";

    public const string OnyxExeName = "onyx.exe";
    public const string OnyxExeNameLinux = "onyx";

    public const string BetterGh3Folder = "BetterGH3";

    // Bundled resource folders (exact casing; case-sensitive on Linux)
    public const string ReplacementsFolder = "Replacements";
    public const string ResourcesFolder = "Resources";
    public const string ListFilesFolder = "List Files";
    public const string SkeletonsFilename = "Skeletons.txt";
    public const string SongCategoriesFilename = "SongCategories.txt";

    // Per-user data subfolders
    public const string BackupsFolder = "Backups";
    public const string TemplatesFolder = "Templates";
    public const string LogsFolder = "Logs";

    public static string GetExeName(string game) =>
        game == GameNames.GH3 ? Gh3ExeName : GhaExeName;

    public static string GetGameDisplayName(string game) =>
        game == GameNames.GH3 ? "Guitar Hero III" : "Guitar Hero Aerosmith";
}
