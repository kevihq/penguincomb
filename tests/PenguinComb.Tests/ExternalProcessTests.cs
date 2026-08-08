using PenguinComb.Infrastructure;
using Xunit;

namespace PenguinComb.Tests;

public class ExternalProcessTests
{
    private static ExternalProcessService CreateService(FakePlatformService? platform = null)
        => new(platform ?? new FakePlatformService { OsKind = "Linux" });

    /// <summary>
    /// Returns a (executable, arguments) pair that runs a small script snippet on the
    /// current OS without going through any user shell. cmd.exe on Windows, /bin/sh
    /// elsewhere. Both are given distinct scripts because their command syntax differs.
    /// </summary>
    private static (string Executable, string[] Arguments) Shell(string linuxScript, string windowsScript)
        => OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/d", "/s", "/c", windowsScript])
            : ("/bin/sh", ["-c", linuxScript]);

    [Fact]
    public async Task Run_ReturnsOutputAndExitCode()
    {
        var service = CreateService();
        var (exe, args) = Shell("echo hello", "echo hello");
        var result = await service.RunAsync(exe, args);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput.TrimEnd());
    }

    [Fact]
    public async Task Run_CapturesStderr()
    {
        var service = CreateService();
        var (exe, args) = Shell("echo oops 1>&2; exit 3", "echo oops 1>&2 & exit 3");
        var result = await service.RunAsync(exe, args);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
        Assert.Contains("oops", result.StandardError);
    }

    [Fact]
    public async Task Run_HandlesArgumentsWithSpacesAndNonAscii()
    {
        var service = CreateService();
        const string expected = "path with spaces/and ünïcode";

        if (OperatingSystem.IsWindows())
        {
            // cmd.exe mangles output through the console code page, so have the
            // child write the argument it received to a UTF-8 file instead of
            // echoing it. $args[0] is the value, $args[1] the output file.
            string dir = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string outFile = Path.Combine(dir, "out.txt");
            try
            {
                var result = await service.RunAsync("powershell.exe",
                [
                    "-NoProfile", "-Command",
                    "Set-Content -LiteralPath $args[1] -Value $args[0] -Encoding UTF8",
                    expected, outFile,
                ]);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal(expected, await File.ReadAllTextAsync(outFile));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
        else
        {
            // printf receives the raw argument including spaces and unicode -
            // no shell quoting involved
            var result = await service.RunAsync("/bin/sh",
                ["-c", "printf '%s' \"$1\"", "sh", expected]);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task Run_CancellationKillsProcess()
    {
        var service = CreateService();
        var (exe, args) = Shell("sleep 10", "ping -n 10 127.0.0.1 >nul");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RunAsync(exe, args, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task FindExecutableOnPath_Linux_RequiresExecutableBit()
    {
        if (!OperatingSystem.IsLinux())
        {
            return; // POSIX mode bits do not exist on Windows
        }

        string dir = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string exec = Path.Combine(dir, "onyx");
            string nonExec = Path.Combine(dir, "notexec");
            await File.WriteAllTextAsync(exec, "#!/bin/sh\necho hi\n");
            await File.WriteAllTextAsync(nonExec, "hello");
            File.SetUnixFileMode(exec, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var platform = new FakePlatformService { OsKind = "Linux", Environment = { ["PATH"] = dir } };
            var service = CreateService(platform);

            Assert.Equal(exec, await service.FindExecutableOnPathAsync("onyx"));
            Assert.Null(await service.FindExecutableOnPathAsync("notexec"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FindExecutableOnPath_Windows_AcceptsExeExtension()
    {
        string dir = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "onyx.exe"), "binary");
            var platform = new FakePlatformService { OsKind = "Windows", Environment = { ["PATH"] = dir } };
            var service = CreateService(platform);

            string? found = await service.FindExecutableOnPathAsync("onyx");
            Assert.NotNull(found);
            Assert.EndsWith("onyx.exe", found);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
