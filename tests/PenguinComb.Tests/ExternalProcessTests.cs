using PenguinComb.Infrastructure;
using Xunit;

namespace PenguinComb.Tests;

public class ExternalProcessTests
{
    private static ExternalProcessService CreateService(FakePlatformService? platform = null)
        => new(platform ?? new FakePlatformService { OsKind = "Linux" });

    [Fact]
    public async Task Run_ReturnsOutputAndExitCode()
    {
        var service = CreateService();
        var result = await service.RunAsync("/bin/sh", ["-c", "printf 'hello'"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput.TrimEnd());
    }

    [Fact]
    public async Task Run_CapturesStderr()
    {
        var service = CreateService();
        var result = await service.RunAsync("/bin/sh", ["-c", "echo oops 1>&2; exit 3"]);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
        Assert.Contains("oops", result.StandardError);
    }

    [Fact]
    public async Task Run_HandlesArgumentsWithSpacesAndNonAscii()
    {
        var service = CreateService();
        // printf receives the raw argument including spaces and unicode - no shell quoting involved
        var result = await service.RunAsync("/bin/sh", ["-c", "printf '%s' \"$1\"", "sh", "path with spaces/and ünïcode"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("path with spaces/and ünïcode", result.StandardOutput);
    }

    [Fact]
    public async Task Run_CancellationKillsProcess()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RunAsync("/bin/sh", ["-c", "sleep 10"], cancellationToken: cts.Token));
    }

    [Fact]
    public async Task FindExecutableOnPath_Linux_RequiresExecutableBit()
    {
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
