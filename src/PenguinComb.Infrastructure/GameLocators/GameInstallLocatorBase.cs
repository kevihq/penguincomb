using PenguinComb.Application.Abstractions;
using PenguinComb.Application.Services;

namespace PenguinComb.Infrastructure.GameLocators;

/// <summary>
/// Shared game-install lookup behavior: validation plus the manual-browse flow.
/// Platform-specific discovery lives in the subclasses.
/// </summary>
public abstract class GameInstallLocatorBase : IGameInstallLocator
{
    protected readonly IFileDialogService Dialogs;
    protected readonly IUserNotificationService Notifications;
    protected readonly GameInstallValidator Validator;
    protected readonly IPlatformService Platform;

    protected GameInstallLocatorBase(
        IFileDialogService dialogs,
        IUserNotificationService notifications,
        GameInstallValidator validator,
        IPlatformService platform)
    {
        Dialogs = dialogs;
        Notifications = notifications;
        Validator = validator;
        Platform = platform;
    }

    public abstract Task<string?> TryFindExistingAsync(string game, CancellationToken cancellationToken = default);

    public async Task<string> BrowseForGameFolderAsync(string game, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            string? folder = await Dialogs.PickFolderAsync(
                $"Select the {GameConstants.GetGameDisplayName(game)} game folder", cancellationToken: cancellationToken);

            if (folder is null)
            {
                throw new OperationCanceledException("User cancelled the path selection.");
            }

            if (!Directory.Exists(folder))
            {
                await Notifications.ShowErrorAsync("Invalid Path", "The selected path does not exist. Please select a valid path.", cancellationToken);
                continue;
            }

            return folder;
        }
    }

    /// <summary>Validates a candidate folder, returning it when valid.</summary>
    protected bool IsValid(string? folder, string game)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return false;
        }
        return Validator.Validate(folder, game).IsValid;
    }

    protected string? FirstValid(IEnumerable<string> candidates, string game)
    {
        foreach (var candidate in candidates)
        {
            if (IsValid(candidate, game))
            {
                return candidate;
            }
        }
        return null;
    }
}
