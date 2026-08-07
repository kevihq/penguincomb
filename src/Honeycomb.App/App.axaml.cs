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

            var mainWindow = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            var settings = Services.GetRequiredService<Honeycomb.Application.Abstractions.ISettingsService>();
            settings.LoadAsync().GetAwaiter().GetResult();

            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = mainVm;
            mainWindow.Show();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Command-line opening of .ghproj / .sgh files
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                mainVm.OpenInputFile(args[1]);
            }
        }

        base.OnFrameworkInitializationCompleted();
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
            Console.WriteLine($"Honeycomb {typeof(App).Assembly.GetName().Version} started.");
        }
        catch (Exception ex)
        {
            // Logging must never prevent startup.
            Console.WriteLine($"Failed to configure logging: {ex.Message}");
        }
    }
}
