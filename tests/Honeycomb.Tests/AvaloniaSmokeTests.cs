using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Honeycomb.App;
using Honeycomb.App.ViewModels;
using Honeycomb.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honeycomb.Tests;

/// <summary>
/// Headless smoke tests: application startup, main view creation, navigation to each
/// major view, view-model command creation and settings view loading.
/// Uses the manual headless bootstrap (more reliable than the xunit integration
/// when many tests run in one process).
/// </summary>
[Collection("AppData")]
public class AvaloniaSmokeTests
{
    private static readonly object BootstrapLock = new();
    private static bool _bootstrapped;

    private static void EnsureHeadlessApp()
    {
        if (Avalonia.Application.Current is not null)
        {
            return;
        }

        lock (BootstrapLock)
        {
            if (_bootstrapped)
            {
                return;
            }

            AppBuilder.Configure<Honeycomb.App.App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();

            // Drain the dispatcher so templates apply deterministically.
            Dispatcher.UIThread.RunJobs();

            _bootstrapped = true;
        }
    }

    private static ServiceProvider CreateServices()
    {
        EnsureHeadlessApp();
        // Redirect all per-user data into a temp folder so tests never touch the real profile.
        string overrideRoot = Path.Combine(Path.GetTempPath(), "honeycomb-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("HONEYCOMB_OVERRIDE_DATA_ROOT", overrideRoot);
        return Honeycomb.App.ServiceCollectionExtensions.BuildServiceProvider();
    }

    private static void ApplyTemplate(Window window)
    {
        window.Show();
        window.ApplyTemplate();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void App_StartsHeadless()
    {
        EnsureHeadlessApp();
        Assert.NotNull(Avalonia.Application.Current);
        Assert.IsType<Honeycomb.App.App>(Avalonia.Application.Current);
    }

    [Fact]
    public void MainWindow_IsCreatedWithViewModel()
    {
        using var services = CreateServices();
        var window = services.GetRequiredService<MainWindow>();
        Assert.NotNull(window);
        Assert.Equal("Guitar Hero Toolkit", window.Title);
    }

    [Fact]
    public void MainWindowViewModel_CommandsAreCreated()
    {
        using var services = CreateServices();
        var vm = services.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(vm.OpenCompileSongCommand);
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
        var window = new CompileSongWindow
        {
            DataContext = services.GetRequiredService<CompileSongViewModel>()
        };
        ApplyTemplate(window);
        Assert.NotNull(window.Content);
    }

    [Fact]
    public void OtherWindows_AreCreated()
    {
        using var services = CreateServices();

        var importWindow = new ImportSghWindow { DataContext = services.GetRequiredService<ImportSghViewModel>() };
        ApplyTemplate(importWindow);
        Assert.NotNull(importWindow.Content);

        var pakWindow = new PakToolsWindow { DataContext = services.GetRequiredService<PakToolsViewModel>() };
        ApplyTemplate(pakWindow);
        Assert.NotNull(pakWindow.Content);

        var wadWindow = new WadToolsWindow { DataContext = services.GetRequiredService<WadToolsViewModel>() };
        ApplyTemplate(wadWindow);
        Assert.NotNull(wadWindow.Content);

        var songListWindow = new SongListManagerWindow { DataContext = services.GetRequiredService<SongListManagerViewModel>() };
        ApplyTemplate(songListWindow);
        Assert.NotNull(songListWindow.Content);
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
        var iconUri = new Uri("avares://Honeycomb/Assets/honeycomb.ico");
        Assert.True(Avalonia.Platform.AssetLoader.Exists(iconUri), "App icon resource not found.");
    }
}
