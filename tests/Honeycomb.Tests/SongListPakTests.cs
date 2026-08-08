using GH_Toolkit_Core.Methods;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Regression tests for the fresh-install compile failure
/// ("Song list entry not found: dlc_songlist.qb"): the bundled BetterGH3
/// customs.pak.xen stores its songlist under a hashed entry name that is not in
/// the bundled keys.txt, and the toolkit lookup must match hashed entries.
/// Windows masked this by accumulating checksums in keys_user.txt over time;
/// AppImage users (whose user-key file could not be written before) hit it on
/// the very first compile.
/// </summary>
[Collection("ToolkitDebugState")]
public class SongListPakTests
{
    [Fact]
    public void AddToDownloadList_WorksWithBundledCustomsPak_WithoutUserKeys()
    {
        // Simulate a fresh install: bundled keys.txt is present, but no learned
        // user-key files (a fresh profile, or an AppImage mount that lost them).
        string qbDebug = Path.Combine(AppContext.BaseDirectory, "QBDebug");
        foreach (string name in new[] { "keys_user.txt", "keys_qs_user.txt" })
        {
            string userFile = Path.Combine(qbDebug, name);
            if (File.Exists(userFile))
            {
                File.Delete(userFile);
            }
        }

        string customsPak = Path.Combine(AppContext.BaseDirectory, "Resources", "BetterGH3", "DATA", "customs.pak.xen");
        Assert.True(File.Exists(customsPak), "Bundled BetterGH3 customs.pak.xen not found in test output.");

        // Minimal song-list entry; AddToDownloadList only reads the checksum.
        var entry = new GH_Toolkit_Core.QB.QBStruct.QBStructData();
        entry.AddQbKeyToStruct("checksum", "12345678");
        entry.AddQbKeyToStruct("Title", "Test Song");
        entry.AddQbKeyToStruct("Artist", "Test Artist");

        var (pakData, pabData) = CreateForGame.AddToDownloadList(customsPak, "PC", [entry], "GH3");

        Assert.NotNull(pakData);
        Assert.True(pakData.Length > 0, "The compiled customs PAK must not be empty.");
        Assert.Null(pabData); // GH3 uses a single (non-split) customs pak
    }
}
