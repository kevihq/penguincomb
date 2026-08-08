using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace PenguinComb.App.Views;

public partial class WadToolsWindow : Window
{
    public WadToolsWindow()
    {
        InitializeComponent();

        // Drag & drop support for the path fields (ported from the WinForms form).
        DragDrop.SetAllowDrop(WadFileBox, true);
        WadFileBox.AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(WadFolderBox, true);
        WadFolderBox.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is TextBox box && e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => p is not null).ToList();
            if (files is { Count: > 0 })
            {
                box.Text = files[0];
            }
        }
    }
}
