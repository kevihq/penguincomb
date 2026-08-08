using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PenguinComb.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not ViewModels.MainWindowViewModel vm)
        {
            return;
        }

        vm.ConsoleLines.CollectionChanged += ConsoleLinesOnCollectionChanged;
        ScrollToEndIfAtBottom();
    }

    private void ConsoleLinesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Only stick to the bottom while the user is already there, so a long
        // compile never yanks the view away while they are scrolling back to
        // read earlier messages.
        Dispatcher.UIThread.Post(ScrollToEndIfAtBottom, DispatcherPriority.Background);
    }

    private void ScrollToEndIfAtBottom()
    {
        if (ConsoleList.ItemCount == 0)
        {
            return;
        }

        var scroll = ConsoleList.FindDescendantOfType<ScrollViewer>();
        if (scroll is null)
        {
            return;
        }

        bool atBottom = scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 8;
        if (atBottom)
        {
            ConsoleList.ScrollIntoView(ConsoleList.Items[^1]);
        }
    }
}
