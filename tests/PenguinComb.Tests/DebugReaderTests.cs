using System.Reflection;
using GH_Toolkit_Core.Debug;
using GH_Toolkit_Core.Methods;
using Xunit;

namespace PenguinComb.Tests;

/// <summary>
/// Regression tests for the read-only app-folder failure: AppImage mounts the app
/// folder read-only, so the toolkit's user-key writers must fall back to a
/// per-user writable folder instead of throwing during compilation
/// ("Read-only file system: .../QBDebug/keys_user.txt").
/// </summary>
[Collection("ToolkitDebugState")]
public class DebugReaderTests
{
    [Fact]
    public void UserKeyFolder_FallsBackToPerUserFolder_WhenAppFolderIsReadOnly()
    {
        // Linux-specific (POSIX mode bits); root bypasses file permissions and
        // Windows ACLs are not affected by Unix mode bits.
        if (OperatingSystem.IsWindows() || string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            return;
        }

        string roDir = Path.Combine(Path.GetTempPath(), "penguincomb-ro-" + Guid.NewGuid().ToString("N"));
        string xdgData = Path.Combine(Path.GetTempPath(), "penguincomb-xdg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(roDir);
        Directory.CreateDirectory(xdgData);

        string? oldXdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string oldExeRoot = DebugReaderState.GetExeRootFolder();
        try
        {
            File.SetUnixFileMode(roDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdgData);
            DebugReaderState.SetExeRootFolder(roDir);

            // Constructing DebugData is exactly what threw inside the AppImage
            // ("keys_user.txt" on a read-only filesystem). It must not throw, and
            // the writers must land in the per-user folder.
            var debugDataType = typeof(DebugReader).GetNestedType("DebugData", BindingFlags.NonPublic)!;
            using var debugData = (IDisposable)Activator.CreateInstance(debugDataType, nonPublic: true)!;

            var writer = debugDataType.GetProperty("QbUserWriter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(debugData);
            Assert.NotNull(writer);

            string expected = Path.Combine(xdgData, "PenguinComb", "QBDebug", "keys_user.txt");
            Assert.True(File.Exists(expected), $"User keys should be written to the per-user folder: {expected}");
        }
        finally
        {
            DebugReaderState.SetExeRootFolder(oldExeRoot);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldXdgData);
            try { File.SetUnixFileMode(roDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { }
            try { Directory.Delete(roDir, true); } catch { }
            try { Directory.Delete(xdgData, true); } catch { }
        }
    }
}
