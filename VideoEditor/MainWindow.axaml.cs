using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using LibVLCSharp.Shared;
using System;
using System.Linq;
using Xabe.FFmpeg;

namespace VideoEditor;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _mediaItems = new();
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia; // <-- Added to manage media lifetime

    public MainWindow()
    {
        InitializeComponent();

        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoView.MediaPlayer = _mediaPlayer;
        MediaListBox.ItemsSource = _mediaItems;
    }

    protected override void OnClosed(EventArgs e)
    {
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVLC.Dispose();
        base.OnClosed(e);
    }

    public async void AddMediaButton_Click(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Video Files",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Video Files")
                {
                    Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                _mediaItems.Add(file.Path.LocalPath);
            }
        }
    }

    public void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string path)
        {
            // Dispose the previous media object
            _currentMedia?.Dispose();

            // Create and play the new media
            _currentMedia = new Media(_libVLC, new Uri(path));
            _mediaPlayer.Play(_currentMedia);
            // DO NOT dispose _currentMedia here
        }
    }

    public async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaItems.Count < 2)
        {
            Console.WriteLine("Please add at least two videos to concatenate.");
            return;
        }

        var outputPath = "output.mp4";
        Console.WriteLine("Starting video concatenation...");

        try
        {
            IConversion conversion = await FFmpeg.Conversions.FromSnippet.Concatenate(outputPath, _mediaItems.ToArray());
            await conversion.Start();

            Console.WriteLine($"Concatenation finished! Video saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during concatenation: {ex.Message}");
        }
    }
}
