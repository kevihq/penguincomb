namespace Honeycomb.Application.Abstractions;

/// <summary>Button choices shown by <see cref="IUserNotificationService.ConfirmAsync"/>.</summary>
public enum ConfirmChoice
{
    Yes,
    No,
    Cancel,
}

/// <summary>
/// User-facing messages and confirmations. Implemented by the UI layer (Avalonia
/// dialogs) and faked in tests.
/// </summary>
public interface IUserNotificationService
{
    Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);

    Task ShowWarningAsync(string title, string message, CancellationToken cancellationToken = default);

    Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default);

    /// <summary>Yes/No confirmation. Returns true when the user confirms.</summary>
    Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default);

    /// <summary>Yes/No/Cancel confirmation.</summary>
    Task<ConfirmChoice> ConfirmChoiceAsync(string title, string message, CancellationToken cancellationToken = default);
}
