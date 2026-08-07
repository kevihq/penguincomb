using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Honeycomb.App.Views;

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
        ScrollToEnd();
    }

    private void ConsoleLinesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
    }

    private void ScrollToEnd()
    {
        if (ConsoleList.ItemCount > 0)
        {
            ConsoleList.ScrollIntoView(ConsoleList.Items[^1]);
        }
    }
}
