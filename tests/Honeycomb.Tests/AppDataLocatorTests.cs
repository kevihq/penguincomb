using Honeycomb.Infrastructure;
using Xunit;

namespace Honeycomb.Tests;

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

    [Fact]
    public void Linux_NoXdg_UsesHomeFallbacks()
    {
        var locator = new AppDataLocator(LinuxPlatform());
        Assert.Equal("/home/alice/.config/honeycomb", locator.ConfigDirectory);
        Assert.Equal("/home/alice/.local/share/honeycomb", locator.DataDirectory);
        Assert.Equal("/home/alice/.cache/honeycomb", locator.CacheDirectory);
    }

    [Fact]
    public void Linux_WithXdg_UsesXdgDirectories()
    {
        var platform = LinuxPlatform();
        platform.Environment["XDG_CONFIG_HOME"] = "/xdg/config";
        platform.Environment["XDG_DATA_HOME"] = "/xdg/data";
        platform.Environment["XDG_CACHE_HOME"] = "/xdg/cache";

        var locator = new AppDataLocator(platform);
        Assert.Equal("/xdg/config/honeycomb", locator.ConfigDirectory);
        Assert.Equal("/xdg/data/honeycomb", locator.DataDirectory);
        Assert.Equal("/xdg/cache/honeycomb", locator.CacheDirectory);
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
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Roaming", "Honeycomb"), locator.ConfigDirectory);
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Roaming", "Honeycomb"), locator.DataDirectory);
        Assert.Equal(Path.Combine(@"C:\Users\alice\AppData\Local", "Honeycomb"), locator.CacheDirectory);
    }

    [Fact]
    public void Subdirectories_AreUnderDataDirectory()
    {
        var platform = LinuxPlatform();
        platform.Environment["HOME"] = Path.Combine(Path.GetTempPath(), "honeycomb-tests", Guid.NewGuid().ToString("N"));
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
        var overrideRoot = Path.Combine(Path.GetTempPath(), "honeycomb-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("HONEYCOMB_OVERRIDE_DATA_ROOT", overrideRoot);
        try
        {
            var locator = new AppDataLocator(platform);
            Assert.StartsWith(overrideRoot, locator.ConfigDirectory);
            Assert.StartsWith(overrideRoot, locator.DataDirectory);
            Assert.StartsWith(overrideRoot, locator.CacheDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HONEYCOMB_OVERRIDE_DATA_ROOT", null);
        }
    }
}
