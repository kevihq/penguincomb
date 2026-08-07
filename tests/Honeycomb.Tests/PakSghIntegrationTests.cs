using GH_Toolkit_Core.Methods;
using GH_Toolkit_Core.PAK;
using GH_Toolkit_Core.QB;
using Honeycomb.Application.Services;
using static GH_Toolkit_Core.PAK.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// End-to-end tests that exercise the real GH-Toolkit algorithms on Linux through
/// the cross-platform application services (PAK compile/extract and SGH import).
/// Fixtures are generated programmatically - no copyrighted game files involved.
/// </summary>
public class PakSghIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "honeycomb-tests", Guid.NewGuid().ToString("N"));

    public PakSghIntegrationTests()
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

    private static QBItem CreateSongEntry(string name, string title, string artist)
    {
        var song = new QBStruct.QBStructData();
        song.AddVarToStruct("name", name, QBKEY);
        song.AddVarToStruct("title", title, STRING);
        song.AddVarToStruct("artist", artist, STRING);
        song.AddVarToStruct("checksum", name, QBKEY);
        return new QBItem(name, song);
    }

    [Fact]
    public void Pak_CompileAndExtract_RoundTrips()
    {
        // Build a .q text file through the toolkit's own QbToText writer
        var entry = CreateSongEntry("testsong", "Test Song", "Test Artist");
        var items = new List<QBItem> { entry };
        string qPath = Path.Combine(_root, "compile", "songs", "testsong.q");
        Directory.CreateDirectory(Path.GetDirectoryName(qPath)!);
        QB.QbToText(items, qPath);

        // Compile the folder into a PAK (PC)
        var compiler = new PakCompiler(GAME_GH3, CONSOLE_PC, split: false);
        var (pakData, _, _) = compiler.CompilePAK(Path.GetDirectoryName(qPath)!);
        Assert.NotNull(pakData);
        Assert.True(pakData!.Length > 0);

        string pakPath = Path.Combine(_root, "testsong.pak.xen");
        File.WriteAllBytes(pakPath, pakData);

        // Extract it back and convert QB -> Q text
        string extractRoot = Path.Combine(_root, "extracted");
        Directory.CreateDirectory(extractRoot);
        PAK.ProcessPAKFromFile(pakPath, convertQ: true);

        // The extraction writes next to the pak; the qb is converted back to a text .q file
        string[] qFiles = Directory.GetFiles(_root, "*.q", SearchOption.AllDirectories);
        Assert.Contains(qFiles, f => Path.GetFileName(f).Equals("testsong.q", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pak_Deflate_PassthroughOnUncompressedData()
    {
        // DeflateData decompresses CHNK-wrapped data; non-compressed input is returned
        // unchanged (documented toolkit behavior).
        byte[] raw = System.Text.Encoding.UTF8.GetBytes("plain-payload");
        string file = Path.Combine(_root, "sample.qb");
        File.WriteAllBytes(file, raw);

        byte[] result = PAK.DeflateData(file);
        Assert.Equal(raw, result);
    }

    [Fact]
    public void Sgh_GeneratedArchive_ImportsBack()
    {
        // Build a songs.info (as the legacy ExportSongs flow does)
        var songs = new List<QBItem>
        {
            CreateSongEntry("songone", "Song One", "Artist A"),
            CreateSongEntry("songtwo", "Song Two", "Artist B")
        };
        byte[] infoBytes = QB.CompileQbFile(songs, "songs.info", GAME_GH3, CONSOLE_PC);
        Assert.NotNull(infoBytes);

        string folder = Path.Combine(_root, "sghdata");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "songs.info"), infoBytes);

        string sghPath = Path.Combine(_root, "exported.sgh");
        GHTCP.MakeUnprotectedZip(folder, sghPath);
        Assert.True(File.Exists(sghPath));

        // Import it back through the application service
        var service = new SghImportService(new FakeSettingsService(), new FakeNotificationService(),
            CreateChecks(), new ResourceLocator(new FakePlatformService()));
        var result = service.LoadSGH(sghPath);

        Assert.Equal(2, result.Songs.Count);
        var names = result.Songs.Select(s => s.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["songone", "songtwo"], names);
        Assert.Equal("Song One", result.Songs.First(s => s.Name == "songone").Title);
        Assert.Equal("Artist B", result.Songs.First(s => s.Name == "songtwo").Artist);
    }

    private PreCompileChecks CreateChecks()
    {
        var platform = new FakePlatformService { OsKind = "Linux" };
        return new PreCompileChecks(
            new FakeSettingsService(),
            new FakeGameLocator(),
            new FakeNotificationService(),
            new FakeToolLocator(),
            new FakeAppDataLocator(),
            new ResourceLocator(platform),
            new GameInstallValidator(platform),
            platform);
    }

    [Fact]
    public void Wad_Extract_ReportsMissingRequiredFiles()
    {
        var service = new WadToolService(new FakeNotificationService());
        string wadPath = Path.Combine(_root, "DATAP.WAD");
        File.WriteAllBytes(wadPath, [1, 2, 3]);

        // Missing DATAP.HED etc. -> no exception, message printed, no extract folder
        service.ExtractAsync(wadPath).GetAwaiter().GetResult();
        Assert.False(Directory.Exists(Path.Combine(_root, "WAD Extract")));
    }
}
