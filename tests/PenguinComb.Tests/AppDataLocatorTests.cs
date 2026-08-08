using PenguinComb.Infrastructure;
using Xunit;

namespace PenguinComb.Tests;

[Collection("AppData")]
public class AppDataLocatorTests
{
    private static FakePlatformService LinuxPlatform()
    {
        return new FakePlatformService
        {
            OsKind = "Linux",
            Environment =
            {
                ["HOME"] = "/home/alice"
            }
        };
    }

    // The Linux path resolution runs on every host in CI; Path.Combine uses the
    // host separator, so normalize before comparing against the canonical
    // forward-slash Linux form.
    private static string N(string path) => path.Replace('\\', '/');

    [Fact]
    public void Linux_NoXdg_UsesHomeFallbacks()
    {
        var locator = new AppDataLocator(LinuxPlatform());
        Assert.Equal("/home/alice/.config/penguincomb", N(locator.ConfigDirectory));
        Assert.Equal("/home/alice/.local/share/penguincomb", N(locator.DataDirectory));
        Assert.Equal("/home/alice/.cache/penguincomb", N(locator.CacheDirectory));
    }

    [Fact]
    public void Linux_WithXdg_UsesXdgDirectories()
    {
        var platform = LinuxPlatform();
        platform.Environment["XDG_CONFIG_HOME"] = "/xdg/config";
        platform.Environment["XDG_DATA_HOME"] = "/xdg/data";
        platform.Environment["XDG_CACHE_HOME"] = "/xdg/cache";

        var locator = new AppDataLocator(platform);
        Assert.Equal("/xdg/config/penguincomb", N(locator.ConfigDirectory));
        Assert.Equal("/xdg/data/penguincomb", N(locator.DataDirectory));
        Assert.Equal("/xdg/cache/penguincomb", N(locator.CacheDirectory));
    }

    [Fact]
    public void Windows_UsesAppData()
    {
        var platform = new FakePlatformService
        {
            OsKind = "Windows",
            Environment =
            {
                ["APPDATA"] = @"C:\Users\alice\AppData\Roaming",
                ["LOCALAPPDATA"] = @"C:\Users\alice\AppData\Local"
            }
        };

        var locator = new AppDataLocator(platform);
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Roaming", "PenguinComb"), locator.ConfigDirectory);
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Roaming", "PenguinComb"), locator.DataDirectory);
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Local", "PenguinComb"), locator.CacheDirectory);
    }

    [Fact]
    public void Subdirectories_AreUnderDataDirectory()
    {
        var platform = LinuxPlatform();
        platform.Environment["HOME"] = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
        var locator = new AppDataLocator(platform);

        Assert.StartsWith(locator.DataDirectory, locator.BackupsDirectory);
        Assert.StartsWith(locator.DataDirectory, locator.TemplatesDirectory);
        Assert.StartsWith(locator.DataDirectory, locator.LogsDirectory);
        Assert.Equal("Backups", Path.GetFileName(locator.BackupsDirectory));
    }

    [Fact]
    public void OverrideRoot_RedirectsEverything()
    {
        var platform = LinuxPlatform();
        var overrideRoot = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("PENGUINCOMB_OVERRIDE_DATA_ROOT", overrideRoot);
        try
        {
            var locator = new AppDataLocator(platform);
            Assert.StartsWith(overrideRoot, locator.ConfigDirectory);
            Assert.StartsWith(overrideRoot, locator.DataDirectory);
            Assert.StartsWith(overrideRoot, locator.CacheDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PENGUINCOMB_OVERRIDE_DATA_ROOT", null);
        }
    }
}
