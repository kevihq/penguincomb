using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Models;

namespace Honeycomb.Application.Services;

/// <summary>
/// Locates bundled (read-only) resources in the application base directory.
/// Uses <see cref="AppContext.BaseDirectory"/> only, and preserves exact file casing.
/// </summary>
public class ResourceLocator
{
    private readonly IPlatformService _platform;

    public ResourceLocator(IPlatformService platform)
    {
        _platform = platform;
    }

    public string AppDirectory => AppContext.BaseDirectory;

    /// <summary>List Files/Skeletons.txt (exact casing).</summary>
    public string SkeletonsPath => Path.Combine(AppDirectory, GameConstants.ListFilesFolder, GameConstants.SkeletonsFilename);

    /// <summary>List Files/SongCategories.txt (exact casing).</summary>
    public string SongCategoriesPath => Path.Combine(AppDirectory, GameConstants.ListFilesFolder, GameConstants.SongCategoriesFilename);

    /// <summary>Replacements/&lt;platform&gt;/&lt;game&gt;/QB - bundled replacement qb files.</summary>
    public string ReplacementsPath => Path.Combine(AppDirectory, GameConstants.ReplacementsFolder);

    /// <summary>Resources/ - bundled game resources from GH-Toolkit.</summary>
    public string ResourcesPath => Path.Combine(AppDirectory, GameConstants.ResourcesFolder);

    /// <summary>Resources/&lt;game&gt; - per-game resources (blank songlists, scripts).</summary>
    public string GameResourcesPath(string game) => Path.Combine(ResourcesPath, game);

    public string BetterGh3Path => Path.Combine(ResourcesPath, GameConstants.BetterGh3Folder);

    public string Ps3ResourcesPath => Path.Combine(ResourcesPath, "PS3");

    public string OnyxResourcesPath => Path.Combine(ResourcesPath, "Onyx");

    /// <summary>Reads a bundled list file. Returns an empty array when missing.</summary>
    public string[] ReadListFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Resource file not found: {path}");
            return [];
        }
        return File.ReadAllLines(path);
    }

    /// <summary>Location of a RemoveReadOnly helper if bundled (Windows only).</summary>
    public string? RemoveReadOnlyToolPath =>
        Path.Combine(AppDirectory, "Tools", "RemoveReadOnly", "RemoveReadOnly.exe");
}
