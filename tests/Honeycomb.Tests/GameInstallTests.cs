using Honeycomb.Application.Models;
using Honeycomb.Application.Services;
using Honeycomb.Infrastructure.GameLocators;
using Xunit;

namespace Honeycomb.Tests;

public class GameInstallTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "honeycomb-tests", Guid.NewGuid().ToString("N"));

    public GameInstallTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>Creates a minimal valid GH3 install layout (data folders + qb pak).</summary>
    private string CreateGh3Install(bool withExe = true, bool withData = true)
    {
        string game = Path.Combine(_root, "Guitar Hero III");
        Directory.CreateDirectory(Path.Combine(game, "DATA", "PAK"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "MUSIC"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "SONGS"));
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pab.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "DATA", "patch.pak.xen"), [1, 2, 3]);
        if (withExe)
        {
            File.WriteAllBytes(Path.Combine(game, "GH3.exe"), [1, 2, 3]);
        }
        return game;
    }

    private static GameInstallValidator CreateValidator(FakePlatformService platform)
        => new(platform);

    [Fact]
    public void Validate_AcceptsDataLayoutWithoutExe_OnLinux()
    {
        // Wine-prefix style: data folders present, Windows exe present
        string game = CreateGh3Install();
        var validator = CreateValidator(new FakePlatformService { OsKind = "Linux" });

        var info = validator.Validate(game, GameNames.GH3);
        Assert.True(info.IsValid);
        Assert.True(info.ExecutableFound);
        Assert.True(info.DataFolderFound);
        Assert.True(info.PakFolderFound);
        Assert.True(info.QbPakFound);
        Assert.True(info.MusicFolderFound);
        Assert.True(info.SongsFolderFound);
        Assert.Empty(info.MissingItems);
    }

    [Fact]
    public void Validate_RejectsMissingData_OnLinux_EvenWithExe()
    {
        string game = Path.Combine(_root, "Ghost");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "GH3.exe"), [1, 2, 3]);

        var validator = CreateValidator(new FakePlatformService { OsKind = "Linux" });
        var info = validator.Validate(game, GameNames.GH3);

        Assert.False(info.IsValid);
        Assert.True(info.ExecutableFound);
        Assert.Contains("DATA", info.MissingItems);
    }

    [Fact]
    public void Validate_AcceptsExeOnly_OnWindows()
    {
        string game = Path.Combine(_root, "Ghost");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "GH3.exe"), [1, 2, 3]);

        var validator = CreateValidator(new FakePlatformService { OsKind = "Windows" });
        var info = validator.Validate(game, GameNames.GH3);

        Assert.True(info.IsValid);
    }

    [Fact]
    public void Validate_RejectsNonExistentFolder()
    {
        var validator = CreateValidator(new FakePlatformService { OsKind = "Linux" });
        var info = validator.Validate(Path.Combine(_root, "nope"), GameNames.GH3);
        Assert.False(info.IsValid);
        Assert.Contains("Folder does not exist", info.MissingItems);
    }

    [Fact]
    public void Validate_Gha_UsesAerosmithExe()
    {
        string game = Path.Combine(_root, "Guitar Hero Aerosmith");
        Directory.CreateDirectory(Path.Combine(game, "DATA", "PAK"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "MUSIC"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "SONGS"));
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "Guitar Hero Aerosmith.exe"), [1, 2, 3]);

        var validator = CreateValidator(new FakePlatformService { OsKind = "Linux" });
        Assert.True(validator.Validate(game, GameNames.GHA).IsValid);
        // GH3 validation passes on the data layout but does not see the GHA executable
        Assert.False(validator.Validate(game, GameNames.GH3).ExecutableFound);
    }

    [Fact]
    public async Task LinuxLocator_FindsGameInWinePrefix()
    {
        // ~/.wine/drive_c/Program Files (x86)/Activision/Guitar Hero III
        string wineGame = Path.Combine(_root, ".wine", "drive_c", "Program Files (x86)", "Activision", "Guitar Hero III");
        Directory.CreateDirectory(Path.Combine(wineGame, "DATA", "PAK"));
        Directory.CreateDirectory(Path.Combine(wineGame, "DATA", "MUSIC"));
        Directory.CreateDirectory(Path.Combine(wineGame, "DATA", "SONGS"));
        File.WriteAllBytes(Path.Combine(wineGame, "DATA", "PAK", "qb.pak.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(wineGame, "GH3.exe"), [1, 2, 3]);

        var platform = new FakePlatformService
        {
            OsKind = "Linux",
            UserName = "tester",
            Environment = { ["HOME"] = _root }
        };
        var locator = new LinuxGameInstallLocator(new FakeDialogService(), new FakeNotificationService(),
            new GameInstallValidator(platform), platform);

        string? found = await locator.TryFindExistingAsync(GameNames.GH3);
        Assert.NotNull(found);
        Assert.Equal(wineGame, Path.GetFullPath(found));
    }

    [Fact]
    public async Task LinuxLocator_FindsGameInSteamCompatData()
    {
        // ~/.local/share/Steam/steamapps/compatdata/123/pfx/drive_c/... 
        string game = Path.Combine(_root, ".local", "share", "Steam", "steamapps", "compatdata", "12345", "pfx", "drive_c",
            "Program Files (x86)", "Aspyr", "Guitar Hero III");
        Directory.CreateDirectory(Path.Combine(game, "DATA", "PAK"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "MUSIC"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "SONGS"));
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "GH3.exe"), [1, 2, 3]);

        var platform = new FakePlatformService
        {
            OsKind = "Linux",
            UserName = "tester",
            Environment = { ["HOME"] = _root }
        };
        var locator = new LinuxGameInstallLocator(new FakeDialogService(), new FakeNotificationService(),
            new GameInstallValidator(platform), platform);

        string? found = await locator.TryFindExistingAsync(GameNames.GH3);
        Assert.NotNull(found);
    }

    [Fact]
    public async Task LinuxLocator_ReturnsNull_WhenNothingFound()
    {
        var platform = new FakePlatformService
        {
            OsKind = "Linux",
            UserName = "tester",
            Environment = { ["HOME"] = _root }
        };
        var locator = new LinuxGameInstallLocator(new FakeDialogService(), new FakeNotificationService(),
            new GameInstallValidator(platform), platform);

        Assert.Null(await locator.TryFindExistingAsync(GameNames.GH3));
        Assert.Null(await locator.TryFindExistingAsync(GameNames.GHA));
    }

    [Fact]
    public async Task BrowseForGameFolder_ThrowsOnCancel()
    {
        var dialogs = new FakeDialogService { Cancels = true };
        var platform = new FakePlatformService { OsKind = "Linux" };
        var locator = new LinuxGameInstallLocator(dialogs, new FakeNotificationService(),
            new GameInstallValidator(platform), platform);

        await Assert.ThrowsAsync<OperationCanceledException>(() => locator.BrowseForGameFolderAsync(GameNames.GH3));
    }

    [Fact]
    public async Task BrowseForGameFolder_ReturnsSelectedFolder()
    {
        string folder = Path.Combine(_root, "picked");
        Directory.CreateDirectory(folder);
        var dialogs = new FakeDialogService { NextFolder = folder };
        var platform = new FakePlatformService { OsKind = "Linux" };
        var locator = new LinuxGameInstallLocator(dialogs, new FakeNotificationService(),
            new GameInstallValidator(platform), platform);

        string result = await locator.BrowseForGameFolderAsync(GameNames.GH3);
        Assert.Equal(folder, result);
    }
}
