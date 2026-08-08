using PenguinComb.Application.Abstractions;
using PenguinComb.Infrastructure;
using Xunit;

namespace PenguinComb.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));

    public SettingsTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // best effort
        }
    }

    private static FakePlatformService LinuxPlatform() => new() { OsKind = "Linux" };

    private JsonSettingsService CreateService(string fileName = "settings.json")
        => new(Path.Combine(_dir, fileName), LinuxPlatform());

    [Fact]
    public void Defaults_MatchLegacyValues()
    {
        var settings = new AppSettings();
        Assert.Equal(1m, settings.PreviewFadeIn);
        Assert.Equal(1m, settings.PreviewFadeOut);
        Assert.True(settings.ShowPostCompile);
        Assert.True(settings.EncryptAudio);
        Assert.True(settings.OverrideBeatLines);
        Assert.True(settings.SongManagerDeleteSongs);
        Assert.True(settings.RecompileQb);
        Assert.True(settings.DlcName);
        Assert.Equal("Xbox 360", settings.PreferredConsole);
        Assert.Equal(0, settings.ChecksumWarning);
        Assert.Equal("", settings.Gh3FolderPath);
        Assert.Equal("", settings.GhaFolderPath);
        Assert.Equal("", settings.WtModsFolder);
        Assert.Equal("", settings.OnyxCliPath);
        Assert.False(settings.Gh3Plus);
        Assert.False(settings.CompileToFolder);
        Assert.Equal(1, settings.Version);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAllValues()
    {
        var service = CreateService();
        service.Settings.PreviewFadeIn = 2.5m;
        service.Settings.PreviewFadeOut = 0.75m;
        service.Settings.Gh3FolderPath = "/games/Guitar Hero III";
        service.Settings.GhaFolderPath = "/games/Guitar Hero Aerosmith";
        service.Settings.OnyxCliPath = "/opt/onyx/onyx";
        service.Settings.WtModsFolder = "/games/WT/MODS";
        service.Settings.PreferredConsole = "PS3";
        service.Settings.ChecksumWarning = 2;
        service.Settings.Gh3Plus = true;
        service.Settings.EncryptAudio = false;
        await service.SaveAsync();

        var loaded = CreateService();
        await loaded.LoadAsync();

        Assert.Equal(2.5m, loaded.Settings.PreviewFadeIn);
        Assert.Equal(0.75m, loaded.Settings.PreviewFadeOut);
        Assert.Equal("/games/Guitar Hero III", loaded.Settings.Gh3FolderPath);
        Assert.Equal("/games/Guitar Hero Aerosmith", loaded.Settings.GhaFolderPath);
        Assert.Equal("/opt/onyx/onyx", loaded.Settings.OnyxCliPath);
        Assert.Equal("/games/WT/MODS", loaded.Settings.WtModsFolder);
        Assert.Equal("PS3", loaded.Settings.PreferredConsole);
        Assert.Equal(2, loaded.Settings.ChecksumWarning);
        Assert.True(loaded.Settings.Gh3Plus);
        Assert.False(loaded.Settings.EncryptAudio);
    }

    [Fact]
    public async Task MalformedFile_RecoversWithDefaults()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "settings.json"), "{ this is not valid json !!!");

        var service = CreateService();
        await service.LoadAsync();

        Assert.Equal(1m, service.Settings.PreviewFadeIn);
        Assert.Equal("", service.Settings.Gh3FolderPath);
        // The malformed file is preserved as a backup
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json.bak")));
    }

    [Fact]
    public async Task MissingFile_UsesDefaults()
    {
        var service = CreateService();
        await service.LoadAsync();
        Assert.Equal("Xbox 360", service.Settings.PreferredConsole);
    }

    [Fact]
    public async Task UnknownFutureFields_ArePreserved()
    {
        var service = CreateService();
        service.Settings.PreviewFadeIn = 3m;
        await service.SaveAsync();

        // Simulate a newer app version writing an extra field
        var path = Path.Combine(_dir, "settings.json");
        var json = await File.ReadAllTextAsync(path);
        json = json.Replace("}", ",\n  \"FutureFeature\": {\"enabled\": true}\n}", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, json);

        var loaded = CreateService();
        await loaded.LoadAsync();
        Assert.Equal(3m, loaded.Settings.PreviewFadeIn);

        // Saving with the older build must not drop the unknown field
        loaded.Settings.ShowPostCompile = false;
        await loaded.SaveAsync();

        var finalJson = await File.ReadAllTextAsync(path);
        Assert.Contains("FutureFeature", finalJson);
    }

    [Fact]
    public async Task NewlyAddedPropertyMissing_DoesNotClobberExistingValues()
    {
        // Simulate settings written by an older build (no FfmpegPath, no LegacySettingsMigrated)
        string json = """
            {
              "Version": 1,
              "Gh3FolderPath": "/games/gh3",
              "PreviewFadeIn": 4.0,
              "ShowPostCompile": false
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_dir, "settings.json"), json);

        var service = CreateService();
        await service.LoadAsync();

        Assert.Equal("/games/gh3", service.Settings.Gh3FolderPath);
        Assert.Equal(4m, service.Settings.PreviewFadeIn);
        Assert.False(service.Settings.ShowPostCompile);
        // New fields fall back to defaults instead of overwriting existing ones
        Assert.Equal("", service.Settings.FfmpegPath);
        Assert.Equal(1, service.Settings.Version);
    }

    [Fact]
    public void AtomicWrite_ProducesNoTempFileLeftovers()
    {
        var service = CreateService();
        service.SaveAsync().GetAwaiter().GetResult();
        service.SaveAsync().GetAwaiter().GetResult();
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json.tmp")));
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
    }
}
