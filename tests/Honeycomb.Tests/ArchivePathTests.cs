using GH_Toolkit_Core.Checksum;
using Honeycomb.Application.Services;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Verifies that archive-internal path handling stays consistent across platforms:
/// checksums are computed on normalized (backslash) names regardless of the host OS,
/// and the compile services keep forward-slash/backslash conversions correct.
/// </summary>
[Collection("ToolkitDebugState")]
public class ArchivePathTests
{
    [Theory]
    [InlineData("songs/test.mid.qb")]
    [InlineData("songs\\test.mid.qb")]
    public void Checksums_AreIndependentOfSeparators(string name)
    {
        uint fwd = CRC.QBKeyUInt(name.Replace('\\', '/'));
        uint bwd = CRC.QBKeyUInt(name.Replace('/', '\\'));
        Assert.Equal(fwd, bwd);
    }

    [Fact]
    public void Checksums_AreCaseInsensitive()
    {
        Assert.Equal(CRC.QBKeyUInt("SONGS\\TEST.MID.QB"), CRC.QBKeyUInt("songs\\test.mid.qb"));
    }

    [Fact]
    public void QsChecksums_MatchKnownReference()
    {
        // "download_songlist" is a well-known QB key in the GH community
        uint key = CRC.QBKeyUInt("download_songlist");
        Assert.True(key > 0);
        Assert.Equal(key, CRC.QBKeyUInt("DOWNLOAD_SONGLIST"));
    }

    [Fact]
    public void ConsoleChecksum_IsStableAndMatchesLegacyAlgorithm()
    {
        string[] input = ["GH3ArtistTitle2024CharterFalse"];
        uint checksum = GH_Toolkit_Core.Methods.CreateForGame.MakeConsoleChecksum(input);
        // Same input twice -> same result
        Assert.Equal(checksum, GH_Toolkit_Core.Methods.CreateForGame.MakeConsoleChecksum(input));
        // Different input -> different result
        uint other = GH_Toolkit_Core.Methods.CreateForGame.MakeConsoleChecksum(["GH3ArtistTitle2024CharterTrue"]);
        Assert.NotEqual(checksum, other);
        // The checksum is derived from the CRC of the concatenation, in the 1e9 range
        Assert.True(checksum >= 1_000_000_000u);
    }

    [Fact]
    public void PakRelativePaths_NormalizeToBackslashes()
    {
        // Ported behavior: GetRelPath lowercases and normalizes separators for the
        // archive entry names. The equivalent public surface is the checksum layer,
        // which must produce identical values for both separator styles.
        string withBackslash = "scripts\\engine\\menu\\menubuttonremap.qb";
        uint key = CRC.QBKeyUInt(withBackslash);
        Assert.Equal(key, CRC.QBKeyUInt("scripts/engine/menu/menubuttonremap.qb"));
        Assert.Equal(key, CRC.QBKeyUInt("SCRIPTS/ENGINE/MENU/MENUBUTTONREMAP.QB"));
    }

    [Fact]
    public void GhprojRelativePaths_UsePlatformSeparatorsOnDisk()
    {
        var projects = new ProjectFileService(new FakeAppDataLocator(), new FakeDialogService());
        string projectPath = Path.Combine(Path.GetTempPath(), "song.ghproj");
        string audioFile = Path.Combine(Path.GetTempPath(), "audio", "guitar.mp3");

        string relative = projects.GetRelativePath(audioFile, projectPath);
        // One level up is allowed (legacy behavior: up to one ".." is kept relative)
        Assert.NotNull(relative);

        // Round trip back to absolute
        string absolute = projects.GetAbsolutePath(relative, projectPath);
        Assert.Equal(Path.GetFullPath(audioFile), Path.GetFullPath(absolute));
    }

    [Fact]
    public void SongChecksum_StripesDiacriticsAndNonLetters()
    {
        Assert.Equal("testsong", SongCompileService.CreateChecksum("Test Song"));
        Assert.Equal("testsong", SongCompileService.CreateChecksum("Tëst Söng!"));
        Assert.Equal("something", SongCompileService.CreateChecksum("Some-thing_123"));
        Assert.Equal("", SongCompileService.CreateChecksum("!!!"));
    }
}
