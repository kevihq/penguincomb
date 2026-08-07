using Honeycomb.Application.Services;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Verifies that bundled resource files are located with their exact on-disk casing
/// (case-sensitive Linux filesystems) and that the legacy lowercase references are
/// no longer used.
/// </summary>
public class ResourceCasingTests
{
    private readonly ResourceLocator _locator;

    public ResourceCasingTests()
    {
        _locator = new ResourceLocator(new FakePlatformService());
    }

    [Fact]
    public void SkeletonsFile_UsesPascalCasePath()
    {
        Assert.EndsWith(Path.Combine("List Files", "Skeletons.txt"), _locator.SkeletonsPath);
        Assert.True(File.Exists(_locator.SkeletonsPath), $"Expected {_locator.SkeletonsPath} to exist in the app output.");
    }

    [Fact]
    public void SongCategoriesFile_UsesPascalCasePath()
    {
        Assert.EndsWith(Path.Combine("List Files", "SongCategories.txt"), _locator.SongCategoriesPath);
        Assert.True(File.Exists(_locator.SongCategoriesPath), $"Expected {_locator.SongCategoriesPath} to exist in the app output.");
    }

    [Fact]
    public void ReplacementsFolder_UsesExactCasing()
    {
        Assert.EndsWith("Replacements", _locator.ReplacementsPath);
        string ghaMenu = Path.Combine(_locator.ReplacementsPath, "PC", "GHA", "QB", "scripts", "guitar", "menu", "menu_setlist.qb");
        Assert.True(File.Exists(ghaMenu), $"Expected {ghaMenu} to exist in the app output.");
    }

    [Fact]
    public void ListFiles_CanBeRead()
    {
        var skeletons = _locator.ReadListFile(_locator.SkeletonsPath);
        Assert.NotEmpty(skeletons);

        var categories = _locator.ReadListFile(_locator.SongCategoriesPath);
        Assert.NotEmpty(categories);
    }
}
