using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;
using Honeycomb.App.Services;
using Honeycomb.App.ViewModels;
using Honeycomb.App.Views;
using Honeycomb.Infrastructure;
using Honeycomb.Infrastructure.GameLocators;
using Microsoft.Extensions.DependencyInjection;

namespace Honeycomb.App;

public static class ServiceCollectionExtensions
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // ---- Platform / infrastructure ----
        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IAppDataLocator, AppDataLocator>();
        services.AddSingleton<IExternalProcessService, ExternalProcessService>();
        services.AddSingleton<IFilePermissionService, FilePermissionService>();
        services.AddSingleton(sp =>
        {
            var locator = sp.GetRequiredService<IAppDataLocator>();
            return new ConsoleLogSink(Path.Combine(locator.LogsDirectory, "honeycomb.log"));
        });

        services.AddSingleton<ISettingsService>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            var locator = sp.GetRequiredService<IAppDataLocator>();
            return new JsonSettingsService(locator.SettingsFilePath, platform);
        });

        // ---- UI-backed services (implemented in this project) ----
        services.AddSingleton(sp => new WindowAccessor(() => App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null));
        services.AddSingleton<IFileDialogService>(sp => new AvaloniaFileDialogService(
            () => sp.GetRequiredService<WindowAccessor>().Current,
            sp.GetRequiredService<IUserNotificationService>()));
        services.AddSingleton<IUserNotificationService>(sp => new AvaloniaNotificationService(() => sp.GetRequiredService<WindowAccessor>().Current));

        // ---- Game install locators (platform-specific, guarded at runtime) ----
        services.AddSingleton<IGameInstallLocator>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            var dialogs = sp.GetRequiredService<IFileDialogService>();
            var notifications = sp.GetRequiredService<IUserNotificationService>();
            var validator = sp.GetRequiredService<GameInstallValidator>();
            if (platform.IsWindows)
            {
                return new WindowsGameInstallLocator(dialogs, notifications, validator, platform);
            }
            return new LinuxGameInstallLocator(dialogs, notifications, validator, platform);
        });

        // ---- Application services ----
        services.AddSingleton<GameInstallValidator>();
        services.AddSingleton<ResourceLocator>();
        services.AddSingleton<IExternalToolLocator, ExternalToolLocator>();
        services.AddSingleton<ProjectFileService>();
        services.AddSingleton<PreCompileChecks>();
        services.AddSingleton<SongCompileService>();
        services.AddSingleton<SghImportService>();
        services.AddSingleton<PakToolService>();
        services.AddSingleton<WadToolService>();
        services.AddSingleton<SongListService>();

        // ---- View models ----
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<CompileSongViewModel>();
        services.AddTransient<ImportSghViewModel>();
        services.AddTransient<PakToolsViewModel>();
        services.AddTransient<WadToolsViewModel>();
        services.AddTransient<SongListManagerViewModel>();
        services.AddTransient<SettingsViewModel>();

        // ---- Views ----
        services.AddSingleton<MainWindow>();
        services.AddTransient<CompileSongWindow>();
        services.AddTransient<ImportSghWindow>();
        services.AddTransient<PakToolsWindow>();
        services.AddTransient<WadToolsWindow>();
        services.AddTransient<SongListManagerWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }
}

/// <summary>Provides the current main window to UI-backed services.</summary>
public sealed class WindowAccessor
{
    private readonly Func<Window?> _get;

    public WindowAccessor(Func<Window?> get)
    {
        _get = get;
    }

    public Window? Current => _get();
}
