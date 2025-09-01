using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using LibVLCSharp.Shared;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace VideoEditor;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<VideoClip> _timelineClips = new();
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;

    public MainWindow()
    {
        InitializeComponent();

        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoView.MediaPlayer = _mediaPlayer;

        // Bind collections to the UI elements
        MediaListBox.ItemsSource = _timelineClips;
        TimelineItemsControl.ItemsSource = _timelineClips;
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
                new FilePickerFileType("Video Files") { Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov" } },
                FilePickerFileTypes.All
            }
        });

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            try
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(path);
                var duration = mediaInfo.Duration;
                var clip = new VideoClip(path, TimeSpan.Zero, duration);
                _timelineClips.Add(clip);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting media info for {path}: {ex.Message}");
                // Optionally, show an error to the user
            }
        }
    }

    public void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is VideoClip clip)
        {
            _currentMedia?.Dispose();

            // By default, VLC plays the whole file. We need to tell it to play just the clip's segment.
            // We can do this with media options.
            _currentMedia = new Media(_libVLC, new Uri(clip.SourcePath),
                $":start-time={clip.TrimStart.TotalSeconds}",
                $":stop-time={clip.TrimEnd.TotalSeconds}");

            _mediaPlayer.Play(_currentMedia);
        }
    }

    public void SplitButton_Click(object sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is not VideoClip selectedClip)
        {
            Console.WriteLine("Please select a clip to split.");
            return;
        }

        var playbackTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
        // The actual split time within the source video's full timeline
        var splitPoint = selectedClip.TrimStart + playbackTime;

        // Basic validation: ensure the split point is within the clip's duration and not at the edges.
        if (splitPoint <= selectedClip.TrimStart || splitPoint >= selectedClip.TrimEnd)
        {
            Console.WriteLine("Split point must be within the clip.");
            return;
        }

        var originalIndex = _timelineClips.IndexOf(selectedClip);
        if (originalIndex == -1) return;

        // Create the two new clips
        var clipA = new VideoClip(selectedClip.SourcePath, selectedClip.TrimStart, splitPoint);
        var clipB = new VideoClip(selectedClip.SourcePath, splitPoint, selectedClip.TrimEnd);

        // Replace the old clip with the two new ones
        _timelineClips.RemoveAt(originalIndex);
        _timelineClips.Insert(originalIndex, clipA);
        _timelineClips.Insert(originalIndex + 1, clipB);

        Console.WriteLine($"Clip '{selectedClip.DisplayName}' split into two clips at {splitPoint}.");
    }

    public async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timelineClips.Count == 0)
        {
            Console.WriteLine("Please add at least one video to the timeline.");
            return;
        }

        var outputPath = "output.mp4";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "VideoEditor_Temp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        Console.WriteLine("Starting video export...");
        ExportButton.IsEnabled = false;

        try
        {
            var tempClipPaths = new List<string>();
            for (int i = 0; i < _timelineClips.Count; i++)
            {
                var clip = _timelineClips[i];
                var tempClipPath = Path.Combine(tempDirectory, $"{i}.mp4");

                Console.WriteLine($"Trimming clip {i+1}/{_timelineClips.Count}...");

                var conversion = FFmpeg.Conversions.New()
                    .AddParameter($"-ss {clip.TrimStart.TotalSeconds}")
                    .AddParameter($"-to {clip.TrimEnd.TotalSeconds}")
                    .AddParameter($"-i \"{clip.SourcePath}\"")
                    .AddParameter("-c:v copy -c:a copy") // Fast, no re-encoding
                    .SetOutput(tempClipPath);

                await conversion.Start();
                tempClipPaths.Add(tempClipPath);
            }

            Console.WriteLine("All clips trimmed. Concatenating...");

            if (tempClipPaths.Count > 1)
            {
                IConversion conversion = await FFmpeg.Conversions.FromSnippet.Concatenate(outputPath, tempClipPaths.ToArray());
                await conversion.Start();
            }
            else if (tempClipPaths.Count == 1)
            {
                // If there's only one clip, we just move the trimmed file.
                File.Move(tempClipPaths.First(), outputPath, true);
            }

            Console.WriteLine($"Export finished! Video saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during export: {ex.Message}");
        }
        finally
        {
            // Clean up temporary files
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
            ExportButton.IsEnabled = true;
        }
    }
}
