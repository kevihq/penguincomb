using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Models;
using PenguinComb.Application.Services;
using Xunit;

namespace PenguinComb.Tests;

/// <summary>Temp-directory app-data locator for tests.</summary>
public class FakeAppDataLocator : IAppDataLocator
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));

    public string ConfigDirectory => Path.Combine(Root, "config");
    public string DataDirectory => Path.Combine(Root, "data");
    public string CacheDirectory => Path.Combine(Root, "cache");
    public string BackupsDirectory => Path.Combine(DataDirectory, "Backups");
    public string TemplatesDirectory => Path.Combine(DataDirectory, "Templates");
    public string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    public string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");

    public FakeAppDataLocator()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(CacheDirectory);
    }
}

[Collection("ToolkitDebugState")]
public class SongCompileLogicTests : IDisposable
{
    private readonly FakeSettingsService _settings = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakePlatformService _platform = new() { OsKind = "Linux" };
    private readonly FakeAppDataLocator _appData = new();
    private readonly CompileServices _services;

    private sealed class CompileServices
    {
        public required ResourceLocator Resources { get; init; }
        public required GameInstallValidator Validator { get; init; }
        public required ProjectFileService Projects { get; init; }
        public required PreCompileChecks Checks { get; init; }
        public required SongCompileService Compile { get; init; }
    }

    public SongCompileLogicTests()
    {
        var resources = new ResourceLocator(_platform);
        var validator = new GameInstallValidator(_platform);
        var projects = new ProjectFileService(_appData, _dialogs);
        var checks = new PreCompileChecks(_settings, new FakeGameLocator(), _notifications,
            new FakeToolLocator(), _appData, resources, validator, _platform);
        var compile = new SongCompileService(_settings, _notifications,
            new PenguinComb.Infrastructure.ExternalProcessService(_platform), _platform,
            new FakePermissionService(), checks, projects, resources);

        _services = new CompileServices
        {
            Resources = resources,
            Validator = validator,
            Projects = projects,
            Checks = checks,
            Compile = compile
        };
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_appData.Root, true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void GenreLists_MatchLegacySets()
    {
        // Base genres are present in every list
        foreach (string g in new[] { "Rock", "Punk", "Glam Rock", "Black Metal", "Classic Rock", "Pop" })
        {
            Assert.Contains(g, Genres.Wt);
            Assert.Contains(g, Genres.Gh5);
            Assert.Contains(g, Genres.Wor);
        }

        Assert.Contains("Heavy Metal", Genres.Wt);
        Assert.Contains("Goth", Genres.Wt);
        Assert.Contains("Alternative", Genres.Gh5);
        Assert.Contains("Hardcore Punk", Genres.Wor);
        Assert.Contains("Progressive Rock", Genres.Wor);
        Assert.DoesNotContain("Hardcore Punk", Genres.Gh5);

        // Every list ends with "Other" and is sorted before it
        foreach (var list in new[] { Genres.Wt, Genres.Gh5, Genres.Wor })
        {
            Assert.Equal("Other", list[^1]);
            for (int i = 0; i < list.Count - 2; i++)
            {
                Assert.True(string.CompareOrdinal(list[i], list[i + 1]) <= 0);
            }
        }
    }

    [Fact]
    public void ProjectRoundTrip_PreservesFieldsAndCompat()
    {
        var data = new SongProjectData
        {
            songName = "mysong",
            title = "My Song",
            artist = "Someone",
            chartAuthor = "Charter",
            previewStart = 15000,
            previewEnd = 45000,
            hmxHopoVal = 190,
            isCover = true,
            coverArtist = "Cover Artist",
            coverYear = 1999,
            album = "Album",
            venueSource = 1,
            countoff = 2,
            vocalGender = 1,
            sustainThreshold = 0.75m,
            backingPaths = "a.mp3;b.mp3;c.mp3",
            backingPathsGh3 = "d.mp3;e.mp3"
        };

        string json = data.ToJson();
        var loaded = SongProjectData.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal("mysong", loaded!.songName);
        Assert.Equal("My Song", loaded.title);
        Assert.Equal("Someone", loaded.artist);
        Assert.Equal("Charter", loaded.chartAuthor);
        Assert.Equal(15000, loaded.previewStart);
        Assert.Equal(45000, loaded.previewEnd);
        Assert.Equal(190, loaded.hmxHopoVal);
        Assert.True(loaded.isCover);
        Assert.Equal("Cover Artist", loaded.coverArtist);
        Assert.Equal(1999, loaded.coverYear);
        Assert.Equal("Album", loaded.album);
        Assert.Equal(1, loaded.venueSource);
        Assert.Equal(2, loaded.countoff);
        Assert.Equal(1, loaded.vocalGender);
        Assert.Equal(0.75m, loaded.sustainThreshold);
        Assert.Equal("a.mp3;b.mp3;c.mp3", loaded.backingPaths);
        Assert.Equal("d.mp3;e.mp3", loaded.backingPathsGh3);
        // Defaults still apply for unset fields
        Assert.Equal("Default", loaded.gSkeleton);
        Assert.Equal(170, loaded.bandTier == 1 ? 170 : loaded.bandTier);
        Assert.Equal("Modern Rock", loaded.ghwtDrumkit);
    }

    [Fact]
    public void LegacyGhprojJson_LoadsWithDefaults()
    {
        // A minimal .ghproj written by the WinForms app (defaults omitted)
        string json = """
            {
              "gameSelect": "GH3",
              "platformSelect": "PC",
              "songName": "legacysong",
              "title": "Legacy Song",
              "artist": "Old Artist",
              "previewStart": 10000,
              "previewEnd": 40000
            }
            """;

        var loaded = SongProjectData.FromJson(json);
        Assert.NotNull(loaded);
        Assert.Equal("GH3", loaded!.gameSelect);
        Assert.Equal("PC", loaded.platformSelect);
        Assert.Equal("legacysong", loaded.songName);
        Assert.Equal("Legacy Song", loaded.title);
        Assert.Equal("Old Artist", loaded.artist);
        Assert.Equal(10000, loaded.previewStart);
        Assert.Equal(40000, loaded.previewEnd);
        // Fields missing from the file fall back to defaults (note: songYear without a
        // [DefaultValue] attribute resets to 0 under Populate - preserved legacy quirk).
        Assert.Equal(170, loaded.hmxHopoVal);
        Assert.Equal("Default", loaded.gSkeleton);
        Assert.Equal(1, loaded.bandTier);
        Assert.Equal(-7m, loaded.previewVolume);
        Assert.Equal(0, loaded.songYear);
    }

    [Fact]
    public void ProjectFileService_RelativePathConversion_AllPathFields()
    {
        string projectPath = Path.Combine(_appData.DataDirectory, "song.ghproj");
        string audioDir = Path.Combine(_appData.DataDirectory, "audio");
        Directory.CreateDirectory(audioDir);

        var data = new SongProjectData
        {
            projectPath = projectPath,
            kickPath = Path.Combine(audioDir, "kick.mp3"),
            backingPaths = $"{Path.Combine(audioDir, "a.mp3")};{Path.Combine(audioDir, "b.mp3")}",
            guitarPathGh3 = Path.Combine(audioDir, "guitar.mp3"),
            midiPath = Path.Combine(audioDir, "notes.mid")
        };

        _services.Projects.SetAllToRelative(data);
        Assert.StartsWith("audio", data.kickPath);
        Assert.Contains("audio", data.backingPaths);
        Assert.False(Path.IsPathRooted(data.kickPath));

        _services.Projects.SetAllToAbsolute(data);
        Assert.Equal(Path.GetFullPath(Path.Combine(audioDir, "kick.mp3")), Path.GetFullPath(data.kickPath));
        Assert.Equal(Path.GetFullPath(Path.Combine(audioDir, "guitar.mp3")), Path.GetFullPath(data.guitarPathGh3));
        Assert.Equal(Path.GetFullPath(Path.Combine(audioDir, "notes.mid")), Path.GetFullPath(data.midiPath));
    }

    [Fact]
    public void DefaultTemplate_IsCreatedInUserDataDirectory()
    {
        _services.Projects.EnsureDefaultTemplate(new SongProjectData());
        string template = _services.Projects.DefaultTemplatePath;
        Assert.True(File.Exists(template));
        Assert.StartsWith(_appData.DataDirectory, template);
    }

    [Fact]
    public void CompileOptions_RecordSemantics()
    {
        var options = new CompileOptions { CompileToFolder = true };
        var derived = options with { IsExport = true };
        Assert.True(derived.IsExport);
        Assert.True(derived.CompileToFolder);
        Assert.False(options.IsExport);
    }
}

/// <summary>Fake game locator for compile tests.</summary>
public class FakeGameLocator : IGameInstallLocator
{
    public string? Folder { get; set; }

    public Task<string?> TryFindExistingAsync(string game, CancellationToken cancellationToken = default)
        => Task.FromResult(Folder);

    public Task<string> BrowseForGameFolderAsync(string game, CancellationToken cancellationToken = default)
        => Folder is null
            ? Task.FromException<string>(new OperationCanceledException("cancelled"))
            : Task.FromResult(Folder);
}

/// <summary>Fake tool locator for compile tests.</summary>
public class FakeToolLocator : IExternalToolLocator
{
    public string? OnyxPath { get; set; } = "/opt/onyx/onyx";
    public string? FfmpegFolder { get; set; }

    public Task<string?> LocateOnyxAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default)
        => Task.FromResult(OnyxPath);

    public Task<string?> LocateFfmpegAsync(bool browseIfMissing = false, CancellationToken cancellationToken = default)
        => Task.FromResult(FfmpegFolder);

    public Task<ToolAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ToolAvailability(true, true, OnyxPath != null));
}

/// <summary>Fake permission service.</summary>
public class FakePermissionService : IFilePermissionService
{
    public bool Result { get; set; } = true;

    public Task<bool> TryMakeWritableRecursiveAsync(string folder, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Result);
}
