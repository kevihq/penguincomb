using Honeycomb.App.ViewModels;
using Honeycomb.Application.Services;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Regression tests for the UI-freeze fixes: the SGH import pipeline must run its
/// heavy work off the UI thread and no application code may block synchronously on
/// async work (which deadlocks whenever the awaited work needs the dispatcher,
/// e.g. a modal dialog or a file picker).
/// </summary>
public class UiThreadSafetyTests
{
    /// <summary>
    /// The exact flow that froze the app: loading an SGH archive. It must complete
    /// (not hang), leave the view model usable, reset the busy flag and surface the
    /// error through the notification service instead of crashing. Fake services are
    /// used so no Avalonia dispatcher is involved - the test is deterministic.
    /// </summary>
    [Fact]
    public async Task ImportSghViewModel_LoadSghAsync_CompletesAndResetsBusy()
    {
        var notifications = new FakeNotificationService();
        var settings = new FakeSettingsService();
        // LoadSGH is pure extraction/parsing; the checks/resources parameters are
        // only used by the console packaging path, which this test does not touch.
        var service = new SghImportService(settings, notifications, null!, null!);
        var vm = new ImportSghViewModel(new FakeDialogService(), notifications, service);

        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sgh");
        await vm.LoadSghAsync(missing);

        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Songs);
        Assert.Equal(missing, vm.SghPath);
        Assert.Single(notifications.Errors); // error reported, not swallowed
    }

    /// <summary>
    /// Guards against reintroducing synchronous blocking on async work in the
    /// application projects. Blocking calls on the UI thread deadlock whenever the
    /// awaited work needs the dispatcher, e.g. a modal dialog or a file picker.
    /// </summary>
    [Fact]
    public void ApplicationProjects_HaveNoSynchronousBlockingOnAsync()
    {
        string root = FindRepositoryRoot();
        string[] blockingPatterns = [".Wait()", "GetAwaiter().GetResult()"];
        var offenders = new List<string>();

        foreach (string dir in new[] { "src/Honeycomb.App", "src/Honeycomb.Application", "src/Honeycomb.Infrastructure" })
        {
            string projectDir = Path.Combine(root, dir);
            if (!Directory.Exists(projectDir))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(file);
                if (blockingPatterns.Any(content.Contains))
                {
                    offenders.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Synchronous blocking on async work found (deadlock risk on the UI thread):\n" + string.Join("\n", offenders));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Honeycomb.App")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root from " + AppContext.BaseDirectory);
    }
}
