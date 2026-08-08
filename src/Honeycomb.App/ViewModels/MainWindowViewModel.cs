using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Honeycomb.App.Services;
using Honeycomb.App.Views;
using Honeycomb.Application.Abstractions;
using Honeycomb.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Honeycomb.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ResourceLocator _resources;

    public ObservableCollection<string> ConsoleLines { get; }

    public string Version { get; } = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    [ObservableProperty]
    private string _ffmpegWarning = "";

    public MainWindowViewModel(
        IServiceProvider services,
        ResourceLocator resources,
        IExternalToolLocator toolLocator)
    {
        _services = services;
        _resources = resources;

        var sink = services.GetService<ConsoleLogSink>();
        ConsoleLines = sink?.Lines ?? [];

        // Availability check is fast, but never block startup on it.
        _ = CheckToolsAsync(toolLocator);
    }

    private async Task CheckToolsAsync(IExternalToolLocator toolLocator)
    {
        try
        {
            var availability = await toolLocator.CheckAvailabilityAsync();
            if (!availability.FfmpegFound)
            {
                FfmpegWarning = "Note: ffmpeg was not found on your PATH. Audio compilation requires ffmpeg/ffprobe (see Settings).";
            }
            else if (!availability.FfprobeFound)
            {
                FfmpegWarning = "Note: ffprobe was not found on your PATH. Audio compilation requires ffmpeg/ffprobe (see Settings).";
            }
        }
        catch (Exception ex)
        {
            // Availability check must never break startup
            Console.WriteLine($"Tool availability check failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenCompileSong(string? inputFile = null)
    {
        var vm = _services.GetRequiredService<CompileSongViewModel>();
        if (!string.IsNullOrEmpty(inputFile))
        {
            await vm.LoadProjectAsync(inputFile);
        }
        var window = new CompileSongWindow { DataContext = vm };
        window.Show();
    }

    [RelayCommand]
    private void OpenImportSgh(string? inputFile = null)
    {
        var vm = _services.GetRequiredService<ImportSghViewModel>();
        if (!string.IsNullOrEmpty(inputFile))
        {
            // Loading is async and runs off the UI thread; never block the click handler.
            _ = vm.LoadSghAsync(inputFile);
        }
        var window = new ImportSghWindow { DataContext = vm };
        window.Show();
    }

    [RelayCommand]
    private void OpenPakTools()
    {
        var window = new PakToolsWindow { DataContext = _services.GetRequiredService<PakToolsViewModel>() };
        window.Show();
    }

    [RelayCommand]
    private void OpenWadTools()
    {
        var window = new WadToolsWindow { DataContext = _services.GetRequiredService<WadToolsViewModel>() };
        window.Show();
    }

    [RelayCommand]
    private void OpenSongListManager()
    {
        var window = new SongListManagerWindow { DataContext = _services.GetRequiredService<SongListManagerViewModel>() };
        window.Show();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new SettingsWindow { DataContext = _services.GetRequiredService<SettingsViewModel>() };
        window.ShowDialog(GetMainWindow());
    }

    private Window? GetMainWindow() =>
        App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    /// <summary>Opens a .ghproj or .sgh file passed on the command line.</summary>
    public void OpenInputFile(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        if (ext == ".ghproj")
        {
            OpenCompileSong(path);
        }
        else if (ext == ".sgh")
        {
            OpenImportSgh(path);
        }
    }
}
