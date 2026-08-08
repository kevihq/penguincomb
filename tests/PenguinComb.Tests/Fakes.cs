using PenguinComb.Application.Abstractions;

namespace PenguinComb.Tests;

/// <summary>Fake platform service with controllable OS/environment behavior.</summary>
public class FakePlatformService : IPlatformService
{
    public string OsKind { get; init; } = "Linux";
    public bool IsWindows => OsKind == "Windows";
    public bool IsLinux => OsKind == "Linux";
    public string UserName { get; init; } = "testuser";
    public Dictionary<string, string> Environment { get; } = new();

    public string GetEnvironmentVariable(string name, string fallback)
        => Environment.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

    public IReadOnlyList<string> GetPathDirectories()
        => (Environment.TryGetValue("PATH", out var path) ? path : "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>In-memory settings service for tests.</summary>
public class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; set; } = new();
    public event EventHandler? SettingsChanged;
    public int SaveCount { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

/// <summary>Fake dialog service; returns pre-programmed results.</summary>
public class FakeDialogService : IFileDialogService
{
    public string? NextFile { get; set; }
    public string? NextFolder { get; set; }
    public string? NextSaveFile { get; set; }
    public List<string> NextFiles { get; set; } = new();
    public List<string> NextFolders { get; set; } = new();
    public bool Cancels { get; set; }

    public Task<string?> PickOpenFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult(Cancels ? null : NextFile);

    public Task<IReadOnlyList<string>> PickOpenFilesAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Cancels ? new List<string>() : NextFiles);

    public Task<string?> PickSaveFileAsync(FileDialogOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult(Cancels ? null : NextSaveFile);

    public Task<string?> PickFolderAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Cancels ? null : NextFolder);

    public Task<IReadOnlyList<string>> PickFoldersAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Cancels ? new List<string>() : NextFolders);
}

/// <summary>Fake notification service; records the last message.</summary>
public class FakeNotificationService : IUserNotificationService
{
    public List<string> Messages { get; } = new();
    public List<string> Errors { get; } = new();
    public bool ConfirmResult { get; set; } = true;
    public ConfirmChoice ConfirmChoiceResult { get; set; } = ConfirmChoice.Yes;

    public Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
        => Task.FromResult(ConfirmResult);

    public Task<ConfirmChoice> ConfirmChoiceAsync(string title, string message, CancellationToken cancellationToken = default)
        => Task.FromResult(ConfirmChoiceResult);
}
