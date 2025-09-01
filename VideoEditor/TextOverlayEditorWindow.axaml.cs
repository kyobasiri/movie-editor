using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace VideoEditor;

public partial class TextOverlayEditorWindow : Window
{
    public TextOverlay Overlay { get; }

    // Helper properties for binding TimeSpan to NumericUpDown
    public decimal StartTimeSeconds
    {
        get => (decimal)Overlay.StartTime.TotalSeconds;
        set => Overlay.StartTime = TimeSpan.FromSeconds((double)value);
    }

    public decimal EndTimeSeconds
    {
        get => (decimal)Overlay.EndTime.TotalSeconds;
        set => Overlay.EndTime = TimeSpan.FromSeconds((double)value);
    }

    public TextOverlayEditorWindow()
    {
        InitializeComponent();
        Overlay = new TextOverlay();
        DataContext = this;
    }

    public TextOverlayEditorWindow(TextOverlay overlay)
    {
        InitializeComponent();
        Overlay = overlay;
        DataContext = this;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // The bindings should have already updated the Overlay object.
        Close(true); // Close and return true result
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close(false); // Close and return false result
    }
}
