using Avalonia.Controls;
using Honeycomb.App.ViewModels;

namespace Honeycomb.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.LoadFromSettings();
                vm.CloseRequested += (_, _) => Close();
            }
        };
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is SettingsViewModel { HasUnsavedChanges: true } vm)
        {
            e.Cancel = true;
            var dialog = new MessageDialog("Unsaved Changes",
                "You have unsaved changes. Do you want to close without saving?", "Save & Close", "Discard");
            var choice = await ShowDialog<object?>(dialog);
            if (choice is true)
            {
                await vm.SaveAsync();
            }
            Close();
            return;
        }
        base.OnClosing(e);
    }
}
