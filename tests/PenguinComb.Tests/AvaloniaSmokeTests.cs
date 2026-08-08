using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PenguinComb.App.ViewModels;
using PenguinComb.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PenguinComb.Tests;

/// <summary>
/// Headless smoke tests: application startup, main view creation, navigation to each
/// major view, view-model command creation and settings view loading.
///
/// The headless Avalonia platform is bootstrapped on a single dedicated UI thread
/// that keeps pumping the dispatcher. Every operation that touches Avalonia controls
/// (window creation, template application) runs on that thread via <see cref="OnUiThread{T}"/>,
/// so the tests are immune to xunit's thread-pool scheduling.
/// </summary>
[Collection("AppData")]
public class AvaloniaSmokeTests
{
    private static readonly object UiThreadStartLock = new();
    private static volatile bool _uiThreadReady;
    private static Exception? _bootstrapError;

    private static void EnsureHeadlessApp()
    {
        EnsureUiThreadStarted();
        if (_bootstrapError is not null)
        {
            throw new InvalidOperationException("Headless Avalonia bootstrap failed.", _bootstrapError);
        }
    }

    /// <summary>Starts the dedicated UI thread (once) and waits until the app is bootstrapped.</summary>
    private static void EnsureUiThreadStarted()
    {
        if (_uiThreadReady)
        {
            return;
        }

        lock (UiThreadStartLock)
        {
            if (_uiThreadReady)
            {
                return;
            }

            var ready = new TaskCompletionSource();
            var thread = new Thread(() =>
            {
                try
                {
                    AppBuilder.Configure<PenguinComb.App.App>()
                        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                        .SetupWithoutStarting();
                }
                catch (Exception ex)
                {
                    _bootstrapError = ex;
                }
                finally
                {
                    ready.SetResult();
                }

                // Message loop: keep pumping the dispatcher until the process exits.
                while (true)
                {
                    try
                    {
                        Dispatcher.UIThread.RunJobs();
                    }
                    catch
                    {
                        // keep pumping
                    }
                    Thread.Sleep(5);
                }
            })
            {
                IsBackground = true,
                Name = "Headless UI thread"
            };
            thread.Start();
            ready.Task.Wait();

            // Drain once so templates apply deterministically.
            OnUiThread(() => { });
            _uiThreadReady = true;
        }
    }

    /// <summary>Runs an action on the dedicated UI thread and waits for it to finish.</summary>
    private static T OnUiThread<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    private static void OnUiThread(Action action) => OnUiThread(() => { action(); return 0; });

    private static ServiceProvider CreateServices()
    {
        EnsureHeadlessApp();
        // Redirect all per-user data into a temp folder so tests never touch the real profile.
        string overrideRoot = Path.Combine(Path.GetTempPath(), "penguincomb-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("PENGUINCOMB_OVERRIDE_DATA_ROOT", overrideRoot);
        return PenguinComb.App.ServiceCollectionExtensions.BuildServiceProvider();
    }

    private static void ApplyTemplate(Window window) =>
        OnUiThread(() =>
        {
            window.Show();
            window.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
        });

    [Fact]
    public void App_StartsHeadless()
    {
        EnsureHeadlessApp();
        Assert.NotNull(OnUiThread(() => Avalonia.Application.Current));
        Assert.IsType<PenguinComb.App.App>(OnUiThread(() => Avalonia.Application.Current));
    }

    [Fact]
    public void MainWindow_IsCreatedWithViewModel()
    {
        using var services = CreateServices();
        var window = OnUiThread(() => services.GetRequiredService<MainWindow>());
        Assert.NotNull(window);
        Assert.Equal("PenguinComb", OnUiThread(() => window.Title));
    }

    [Fact]
    public void MainWindowViewModel_CommandsAreCreated()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(vm.OpenCompileSongCommand);
        Assert.NotNull(vm.OpenBatchCompileCommand);
        Assert.NotNull(vm.OpenChToGh3Command);
        Assert.NotNull(vm.OpenImportSghCommand);
        Assert.NotNull(vm.OpenPakToolsCommand);
        Assert.NotNull(vm.OpenWadToolsCommand);
        Assert.NotNull(vm.OpenSongListManagerCommand);
        Assert.NotNull(vm.OpenSettingsCommand);
        Assert.NotNull(vm.ConsoleLines);
    }

    [Fact]
    public void CompileSongViewModel_CommandsAreCreated()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<CompileSongViewModel>();

        Assert.NotNull(vm.CompileAllCommand);
        Assert.NotNull(vm.CompilePaksCommand);
        Assert.NotNull(vm.SaveCommand);
        Assert.NotNull(vm.SaveAsCommand);
        Assert.NotNull(vm.OpenCommand);
        Assert.NotNull(vm.NewProjectCommand);
        Assert.NotNull(vm.CompileAudioCommand);
        Assert.NotNull(vm.ExportSongArchiveCommand);
        Assert.NotNull(vm.OpenSettingsCommand);
        Assert.NotNull(vm.ImportChFolderCommand);
        Assert.NotNull(vm.BrowseCommand);
        Assert.NotNull(vm.AddBackingCommand);
        Assert.NotNull(vm.RemoveBackingCommand);

        Assert.Equal("GH3", vm.SelectedGame);
        Assert.Equal("PC", vm.SelectedPlatform);
        Assert.True(vm.IsGh3TabsVisible);
        Assert.False(vm.IsModernTabsVisible);
        Assert.Equal("Export to SGH", vm.CompileButtonText);
    }

    [Fact]
    public void CompileSongViewModel_GameSwitching_UpdatesTabs()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<CompileSongViewModel>();

        vm.SelectedGame = "GHWT";
        Assert.True(vm.IsModernTabsVisible);
        Assert.False(vm.IsGh3TabsVisible);
        Assert.True(vm.IsWtdeSettingsVisible);
        Assert.False(vm.IsGh5SettingsVisible);
        Assert.False(vm.Platform360Enabled);
        Assert.False(vm.PlatformPs3Enabled);
        Assert.Equal("PC", vm.SelectedPlatform);

        vm.SelectedGame = "GH5";
        Assert.True(vm.IsGh5SettingsVisible);
        Assert.False(vm.IsWtdeSettingsVisible);
        Assert.False(vm.PlatformPcEnabled);

        vm.SelectedGame = "GH3";
        Assert.True(vm.IsGh3TabsVisible);
        Assert.True(vm.PlatformPs2Enabled);
    }

    [Fact]
    public void CompileSongWindow_IsCreated()
    {
        using var services = CreateServices();
        var window = OnUiThread(() => new CompileSongWindow
        {
            DataContext = services.GetRequiredService<CompileSongViewModel>()
        });
        ApplyTemplate(window);
        Assert.NotNull(OnUiThread(() => window.Content));
    }

    [Fact]
    public void OtherWindows_AreCreated()
    {
        using var services = CreateServices();

        var importWindow = OnUiThread(() => new ImportSghWindow { DataContext = services.GetRequiredService<ImportSghViewModel>() });
        ApplyTemplate(importWindow);
        Assert.NotNull(OnUiThread(() => importWindow.Content));

        var batchWindow = OnUiThread(() => new BatchCompileWindow { DataContext = services.GetRequiredService<BatchCompileViewModel>() });
        ApplyTemplate(batchWindow);
        Assert.NotNull(OnUiThread(() => batchWindow.Content));

        var chToGh3Window = OnUiThread(() => new ChToGh3Window { DataContext = services.GetRequiredService<ChToGh3ViewModel>() });
        ApplyTemplate(chToGh3Window);
        Assert.NotNull(OnUiThread(() => chToGh3Window.Content));

        var pakWindow = OnUiThread(() => new PakToolsWindow { DataContext = services.GetRequiredService<PakToolsViewModel>() });
        ApplyTemplate(pakWindow);
        Assert.NotNull(OnUiThread(() => pakWindow.Content));

        var wadWindow = OnUiThread(() => new WadToolsWindow { DataContext = services.GetRequiredService<WadToolsViewModel>() });
        ApplyTemplate(wadWindow);
        Assert.NotNull(OnUiThread(() => wadWindow.Content));

        var songListWindow = OnUiThread(() => new SongListManagerWindow { DataContext = services.GetRequiredService<SongListManagerViewModel>() });
        ApplyTemplate(songListWindow);
        Assert.NotNull(OnUiThread(() => songListWindow.Content));
    }

    [Fact]
    public void SettingsViewModel_LoadsAndSaves()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<SettingsViewModel>();

        vm.LoadFromSettings();
        Assert.False(vm.HasUnsavedChanges);

        vm.PreviewFadeIn = 2m;
        Assert.True(vm.HasUnsavedChanges);

        bool saved = vm.SaveAsync().GetAwaiter().GetResult();
        Assert.True(saved);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(2m, vm.Pref.PreviewFadeIn);
    }

    [Fact]
    public void App_ResourcesAreAvailable()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<MainWindowViewModel>();
        Assert.NotNull(vm.ConsoleLines);
        Assert.NotNull(vm.FfmpegWarning);

        // The app icon must resolve in the built assembly
        var iconUri = new Uri("avares://PenguinComb/Assets/penguincomb.ico");
        Assert.True(OnUiThread(() => Avalonia.Platform.AssetLoader.Exists(iconUri)), "App icon resource not found.");
    }
}
