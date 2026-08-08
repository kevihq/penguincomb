using Avalonia.Controls;
using Avalonia.Interactivity;
using PenguinComb.Application.Abstractions;

namespace PenguinComb.App.Views;

public partial class MessageDialog : Window
{
    /// <summary>Required by the Avalonia XAML runtime loader; use the parameterized ctors.</summary>
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, bool showCancel = false)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        YesButton.Content = "OK";
        YesButton.IsVisible = true;
        YesButton.Click += (_, _) => Close(true);
        if (showCancel)
        {
            CancelButton.IsVisible = true;
            CancelButton.Click += (_, _) => Close(null);
        }
    }

    public MessageDialog(string title, string message, string yesText, string noText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        YesButton.Content = yesText;
        YesButton.IsVisible = true;
        YesButton.Click += (_, _) => Close(true);
        NoButton.Content = noText;
        NoButton.IsVisible = true;
        NoButton.Click += (_, _) => Close(false);
    }
}
