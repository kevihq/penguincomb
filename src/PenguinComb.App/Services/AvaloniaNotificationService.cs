using Avalonia.Controls;
using PenguinComb.Application.Abstractions;
using PenguinComb.App.Views;

namespace PenguinComb.App.Services;

/// <summary>
/// Modal message/confirmation dialogs hosted on the main window.
/// </summary>
public class AvaloniaNotificationService : IUserNotificationService
{
    private readonly Func<Window?> _windowProvider;

    public AvaloniaNotificationService(Func<Window?> windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
        => ShowAsync(new MessageDialog(title, message));

    public Task ShowWarningAsync(string title, string message, CancellationToken cancellationToken = default)
        => ShowAsync(new MessageDialog(title, message));

    public Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default)
        => ShowAsync(new MessageDialog(title, message));

    public async Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var dialog = new MessageDialog(title, message, "OK", "Cancel");
        var result = await ShowDialogAsync(dialog);
        return result is true;
    }

    public async Task<ConfirmChoice> ConfirmChoiceAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var dialog = new MessageDialog(title, message, "OK", "Cancel");
        var result = await ShowDialogAsync(dialog);
        return result is true ? ConfirmChoice.Yes : result is null ? ConfirmChoice.Cancel : ConfirmChoice.No;
    }

    private Task ShowAsync(Window dialog)
    {
        Window? owner = _windowProvider();
        if (owner is null)
        {
            return Task.CompletedTask;
        }
        return dialog.ShowDialog(owner);
    }

    private Task<object?> ShowDialogAsync(Window dialog)
    {
        Window? owner = _windowProvider();
        if (owner is null)
        {
            return Task.FromResult<object?>(false);
        }
        return dialog.ShowDialog<object?>(owner);
    }
}
