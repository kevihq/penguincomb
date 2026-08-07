using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Honeycomb.App.Views;

public partial class PakToolsWindow : Window
{
    public PakToolsWindow()
    {
        InitializeComponent();

        // Drag & drop support for the path fields (ported from the WinForms form).
        EnableDragDrop(ExtractPathBox);
        EnableDragDrop(CompilePathBox);
    }

    private void EnableDragDrop(TextBox? box)
    {
        if (box is null)
        {
            return;
        }
        DragDrop.SetAllowDrop(box, true);
        box.AddHandler(DragDrop.DropEvent, OnDrop);
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
