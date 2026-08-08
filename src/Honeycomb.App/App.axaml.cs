using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Honeycomb.App.Services;
using Honeycomb.App.ViewModels;
using Honeycomb.App.Views;
using Honeycomb.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Honeycomb.App;

public partial class App : Avalonia.Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = BuildServiceProvider();
            ConfigureLogging();

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            _ = StartupAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartupAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            // Load settings before any view model reads them. Never block the UI
            // thread on this: with the dispatcher sync context installed, waiting
            // synchronously on async I/O can deadlock startup.
            var settings = Services.GetRequiredService<Honeycomb.Application.Abstractions.ISettingsService>();
            await settings.LoadAsync();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = mainVm;
            mainWindow.Show();

            // Command-line opening of .ghproj / .sgh files
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                mainVm.OpenInputFile(args[1]);
            }
        }
        catch (Exception ex)
        {
            // A startup failure must be visible, never a silent hang.
            Console.WriteLine($"Startup failed: {ex}");
            var error = new Avalonia.Controls.TextBlock
            {
                Text = $"Honeycomb failed to start:\n\n{ex.Message}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(16)
            };
            desktop.MainWindow = new Avalonia.Controls.Window
            {
                Title = "Honeycomb - Startup Error",
                Content = error,
                Width = 480,
                Height = 220
            };
            desktop.MainWindow.Show();
        }
    }

    private static ServiceProvider BuildServiceProvider()
    {
        return ServiceCollectionExtensions.BuildServiceProvider();
    }

    private void ConfigureLogging()
    {
        try
        {
            var sink = Services.GetRequiredService<ConsoleLogSink>();
            Console.SetOut(sink);
            Console.SetError(sink);
            Console.WriteLine($"Honeycomb {typeof(App).Assembly.GetName().Version} - created by Kelvin Klein.");
        }
        catch (Exception ex)
        {
            // Logging must never prevent startup.
            Console.WriteLine($"Failed to configure logging: {ex.Message}");
        }
    }
}
