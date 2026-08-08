using PenguinComb.Application.Models;

namespace PenguinComb.Application.Abstractions;

/// <summary>
/// Locates a Guitar Hero installation folder. Windows implementations may use the
/// registry; Linux implementations may search Wine/Proton prefixes. Every platform
/// falls back to manual user selection via <see cref="IFileDialogService"/>.
/// </summary>
public interface IGameInstallLocator
{
    /// <summary>
    /// Tries to locate a game installation without asking the user.
    /// Returns null when automatic discovery finds nothing usable.
    /// </summary>
    /// <param name="game">GAME_GH3 or GAME_GHA.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> TryFindExistingAsync(string game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the user to pick a game folder (loops until a valid folder is chosen).
    /// Throws <see cref="OperationCanceledException"/> when the user cancels.
    /// </summary>
    Task<string> BrowseForGameFolderAsync(string game, CancellationToken cancellationToken = default);
}
