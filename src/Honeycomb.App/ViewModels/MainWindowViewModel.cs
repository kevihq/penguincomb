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

    public string FfmpegWarning { get; }

    public MainWindowViewModel(
        IServiceProvider services,
        ResourceLocator resources,
        IExternalToolLocator toolLocator)
    {
        _services = services;
        _resources = resources;

        var sink = services.GetService<ConsoleLogSink>();
        ConsoleLines = sink?.Lines ?? [];

        string ffmpeg = CheckFfmpeg(toolLocator);
        FfmpegWarning = string.IsNullOrEmpty(ffmpeg)
            ? ""
            : $"Note: {ffmpeg} was not found on your PATH. Audio compilation requires ffmpeg/ffprobe.";
    }

    private static string CheckFfmpeg(IExternalToolLocator toolLocator)
    {
        try
        {
            var availability = toolLocator.CheckAvailabilityAsync().GetAwaiter().GetResult();
            if (!availability.FfmpegFound)
            {
                return "ffmpeg";
            }
            if (!availability.FfprobeFound)
            {
                return "ffprobe";
            }
        }
        catch
        {
            // Availability check must never break startup
        }
        return "";
    }

    [RelayCommand]
    private void OpenCompileSong(string? inputFile = null)
    {
        var vm = _services.GetRequiredService<CompileSongViewModel>();
        if (!string.IsNullOrEmpty(inputFile))
        {
            vm.LoadProject(inputFile);
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
            vm.LoadSgh(inputFile);
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
