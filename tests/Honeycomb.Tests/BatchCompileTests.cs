using GH_Toolkit_Core.QB;
using Honeycomb.Application.Services;
using Honeycomb.Infrastructure;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Tests for the batch compile feature: multiple .ghproj projects compiled in
/// sequence through the real compile pipeline, with per-song error reporting and
/// cancellation. Game fixtures are synthesized folders - no copyrighted files.
/// </summary>
[Collection("ToolkitDebugState")]
public class BatchCompileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "honeycomb-batch", Guid.NewGuid().ToString("N"));
    private readonly BatchFixture _fixture = new();

    private sealed class BatchFixture : IDisposable
    {
        public FakeAppDataLocator AppData { get; } = new();
        public FakeSettingsService Settings { get; } = new();
        public FakeNotificationService Notifications { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public FakePlatformService Platform { get; } = new() { OsKind = "Linux" };
        public FakeGameLocator GameLocator { get; } = new();
        public FakeToolLocator ToolLocator { get; } = new();
        public BatchCompileService Service { get; }

        public BatchFixture()
        {
            var resources = new ResourceLocator(Platform);
            var validator = new GameInstallValidator(Platform);
            var projects = new ProjectFileService(AppData, Dialogs);
            var checks = new PreCompileChecks(Settings, GameLocator, Notifications,
                ToolLocator, AppData, resources, validator, Platform);
            var compile = new SongCompileService(Settings, Notifications,
                new ExternalProcessService(Platform), Platform,
                new FakePermissionService(), checks, projects, resources);
            Service = new BatchCompileService(projects, compile, AppData);
        }

        public void Dispose()
        {
            try { Directory.Delete(AppData.Root, true); } catch { }
        }
    }

    private static string WriteProject(string path, string songName, string title = "Test Song", string artist = "Test Artist")
    {
        var data = new Honeycomb.Application.Models.SongProjectData
        {
            gameSelect = "GH3",
            platformSelect = "PC",
            songName = songName,
            title = title,
            artist = artist,
            projectPath = path
        };
        File.WriteAllText(path, data.ToJson());
        return path;
    }

    private static string CreateValidGh3Folder(string root)
    {
        string game = Path.Combine(root, "gh3");
        Directory.CreateDirectory(Path.Combine(game, "DATA", "PAK"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "MUSIC"));
        Directory.CreateDirectory(Path.Combine(game, "DATA", "SONGS"));
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pak.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "DATA", "PAK", "qb.pab.xen"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(game, "DATA", "patch.pak.xen"), [1, 2, 3]);
        return game;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
        _fixture.Dispose();
    }

    [Fact]
    public async Task Batch_LoadFailures_AreRecordedAndBatchContinues()
    {
        Directory.CreateDirectory(_root);
        string missing = Path.Combine(_root, "missing.ghproj");
        string invalid = Path.Combine(_root, "invalid.ghproj");
        File.WriteAllText(invalid, "{ this is not valid json !!!");

        var results = await _fixture.Service.CompileAllAsync([missing, invalid]);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("missing", results[0].SongName);
        Assert.Contains("Could not read the project file.", results[0].Error);
        Assert.Equal("invalid", results[1].SongName);
        Assert.False(string.IsNullOrEmpty(results[1].Error));
    }

    [Fact]
    public async Task Batch_ProcessesAllProjectsInOrder_EvenWhenSomeFail()
    {
        Directory.CreateDirectory(_root);
        _fixture.GameLocator.Folder = CreateValidGh3Folder(_root);

        string a = WriteProject(Path.Combine(_root, "a.ghproj"), "songa");
        string b = WriteProject(Path.Combine(_root, "b.ghproj"), "songb");

        var updates = new List<BatchCompileUpdate>();
        var results = await _fixture.Service.CompileAllAsync([a, b],
            new Progress<BatchCompileUpdate>(updates.Add));

        // Both projects were attempted, in order, even though neither has a MIDI
        // chart (each fails in the compile step without stopping the batch).
        Assert.Equal(2, results.Count);
        Assert.Equal("songa", results[0].SongName);
        Assert.Equal("songb", results[1].SongName);
        Assert.False(results[0].Success);
        Assert.False(results[1].Success);
        Assert.All(results, r => Assert.False(r.Cancelled));
        Assert.NotEmpty(updates);
        Assert.Contains(updates, u => u.Total == 2);

        // The game preflight ran: BetterGH3's customs.pak.xen was installed.
        Assert.True(File.Exists(Path.Combine(_fixture.GameLocator.Folder!, "DATA", "customs.pak.xen")));
    }

    [Fact]
    public async Task Batch_PreCancelledToken_StopsImmediately()
    {
        Directory.CreateDirectory(_root);
        string a = WriteProject(Path.Combine(_root, "a.ghproj"), "songa");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _fixture.Service.CompileAllAsync([a], cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Batch_ChFolder_IsImportedAsGh3PcProjectAndCompiled()
    {
        Directory.CreateDirectory(_root);
        _fixture.GameLocator.Folder = CreateValidGh3Folder(_root);

        // A minimal Clone Hero song folder: song.ini with a checksum + an empty chart.
        string chFolder = Path.Combine(_root, "Clone Hero", "My Great Song");
        Directory.CreateDirectory(chFolder);
        File.WriteAllText(Path.Combine(chFolder, "song.ini"), """
            [song]
            name = My Great Song
            artist = Someone
            charter = Someone Else
            checksum = mygreatsong
            """);
        File.WriteAllBytes(Path.Combine(chFolder, "notes.mid"), []);

        var results = await _fixture.Service.CompileChFoldersAsync([chFolder]);

        Assert.Single(results);
        Assert.Equal("mygreatsong", results[0].SongName); // checksum from song.ini
        Assert.False(results[0].Success); // no real chart/audio -> fails at compile, without crashing
        Assert.False(results[0].Cancelled);

        // The import was recorded as a GH3 PC project in the per-user data directory.
        string savedProject = Path.Combine(_fixture.AppData.DataDirectory, "Clone Hero Imports", "mygreatsong.ghproj");
        Assert.True(File.Exists(savedProject), "Imported Clone Hero song should be saved as a .ghproj.");
        var saved = Honeycomb.Application.Models.SongProjectData.FromJson(File.ReadAllText(savedProject));
        Assert.NotNull(saved);
        Assert.Equal("GH3", saved!.gameSelect);
        Assert.Equal("PC", saved.platformSelect);
        Assert.Equal("mygreatsong", saved.songName);
    }

    [Fact]
    public async Task Batch_ChFolder_WithoutIni_UsesFolderNameAsSongName()
    {
        Directory.CreateDirectory(_root);
        _fixture.GameLocator.Folder = CreateValidGh3Folder(_root);

        // No song.ini, no chart -> the folder name is used and the compile fails cleanly.
        string chFolder = Path.Combine(_root, "Some Song Folder");
        Directory.CreateDirectory(chFolder);

        var results = await _fixture.Service.CompileChFoldersAsync([chFolder]);

        Assert.Single(results);
        Assert.Equal("Some Song Folder", results[0].SongName);
        Assert.False(results[0].Success);
    }

    [Fact]
    public void BatchViewModel_CloneHeroLibrary_ScansForSongFolders()
    {
        Directory.CreateDirectory(_root);
        string root = Path.Combine(_root, "library");
        Directory.CreateDirectory(Path.Combine(root, "song one"));
        Directory.CreateDirectory(Path.Combine(root, "song two"));
        Directory.CreateDirectory(Path.Combine(root, "not a song"));
        File.WriteAllText(Path.Combine(root, "song one", "song.ini"), "[song]\nname = Song One\n");
        File.WriteAllText(Path.Combine(root, "song two", "song.ini"), "[song]\nname = Song Two\n");

        var dialogs = new FakeDialogService { NextFolder = root };
        var vm = new Honeycomb.App.ViewModels.BatchCompileViewModel(
            dialogs, new FakeNotificationService(), _fixture.Service);

        vm.AddChLibraryCommand.Execute(null);

        Assert.Equal(2, vm.Songs.Count);
        Assert.All(vm.Songs, s => Assert.True(s.IsCloneHero));
        Assert.Contains(vm.Songs, s => s.SongName == "song one");
        Assert.Contains(vm.Songs, s => s.SongName == "song two");
    }

    [Fact]
    public void BatchViewModel_AddChSongs_UsesMultiFolderPicker()
    {
        Directory.CreateDirectory(_root);
        string a = Path.Combine(_root, "folder a");
        string b = Path.Combine(_root, "folder b");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        var dialogs = new FakeDialogService { NextFolders = [a, b] };
        var vm = new Honeycomb.App.ViewModels.BatchCompileViewModel(
            dialogs, new FakeNotificationService(), _fixture.Service);

        vm.AddChSongsCommand.Execute(null);

        Assert.Equal(2, vm.Songs.Count);
        Assert.All(vm.Songs, s => Assert.True(s.IsCloneHero));
        Assert.Contains(vm.Songs, s => s.SongName == "folder a");
        Assert.Contains(vm.Songs, s => s.SongName == "folder b");
    }

    [Fact]
    public void BatchViewModel_AddRemoveClear_ManagesSongList()
    {
        Directory.CreateDirectory(_root);
        string a = WriteProject(Path.Combine(_root, "a.ghproj"), "songa");
        string b = WriteProject(Path.Combine(_root, "b.ghproj"), "songb");

        var dialogs = new FakeDialogService { NextFiles = [a, b] };
        var vm = new Honeycomb.App.ViewModels.BatchCompileViewModel(
            dialogs, new FakeNotificationService(), _fixture.Service);

        vm.AddFilesCommand.Execute(null);
        Assert.Equal(2, vm.Songs.Count);
        Assert.All(vm.Songs, s => Assert.False(s.IsCloneHero));

        // Duplicates are ignored
        vm.AddFilesCommand.Execute(null);
        Assert.Equal(2, vm.Songs.Count);

        vm.SelectedSong = vm.Songs[0];
        vm.RemoveSelectedCommand.Execute(null);
        Assert.Single(vm.Songs);

        vm.ClearCommand.Execute(null);
        Assert.Empty(vm.Songs);
    }
}
