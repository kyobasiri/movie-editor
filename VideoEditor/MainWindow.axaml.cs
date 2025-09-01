using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using LibVLCSharp.Shared;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Xabe.FFmpeg.Streams;

namespace VideoEditor;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ITimelineItem> _timelineClips = new();
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;
    private bool _isUpdatingClipProperties = false;

    public MainWindow()
    {
        InitializeComponent();

        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoView.MediaPlayer = _mediaPlayer;

        MediaListBox.ItemsSource = _timelineClips;
        TimelineItemsControl.ItemsSource = _timelineClips;

        RotationComboBox.ItemsSource = Enum.GetValues(typeof(VideoRotation));
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
            Title = "Open Media Files",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Media Files") { Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.png", "*.jpg", "*.jpeg", "*.bmp" } },
                FilePickerFileTypes.All
            }
        });

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                if (new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(extension))
                {
                    _timelineClips.Add(new ImageClip(path, TimeSpan.FromSeconds(5)));
                }
                else
                {
                    var mediaInfo = await FFmpeg.GetMediaInfo(path);
                    var duration = mediaInfo.Duration;
                    _timelineClips.Add(new VideoClip(path, TimeSpan.Zero, duration));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting media info for {path}: {ex.Message}");
            }
        }
    }

    public void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isUpdatingClipProperties = true;
        try
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ITimelineItem item)
            {
                if (item is VideoClip clip)
                {
                    ClipPropertiesPanel.IsVisible = true;
                    RotationComboBox.SelectedItem = clip.Rotation;
                    SpeedUpDown.Value = (decimal)clip.Speed;

                    _currentMedia?.Dispose();
                    _currentMedia = new Media(_libVLC, new Uri(clip.SourcePath),
                        $":start-time={clip.TrimStart.TotalSeconds}",
                        $":stop-time={clip.TrimEnd.TotalSeconds}",
                        $":rate={clip.Speed}");
                    _mediaPlayer.Play(_currentMedia);
                }
                else if (item is ImageClip)
                {
                    ClipPropertiesPanel.IsVisible = false;
                    _mediaPlayer.Stop();
                    _currentMedia?.Dispose();
                    _currentMedia = null;
                }
            }
            else
            {
                ClipPropertiesPanel.IsVisible = false;
            }
        }
        finally
        {
            _isUpdatingClipProperties = false;
        }
    }

    public void SplitButton_Click(object sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is not VideoClip selectedClip)
        {
            Console.WriteLine("Please select a video clip to split.");
            return;
        }

        var playbackTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
        var splitPoint = selectedClip.TrimStart + playbackTime;

        if (splitPoint <= selectedClip.TrimStart || splitPoint >= selectedClip.TrimEnd)
        {
            Console.WriteLine("Split point must be within the clip.");
            return;
        }

        var originalIndex = _timelineClips.IndexOf(selectedClip);
        if (originalIndex == -1) return;

        var clipA = new VideoClip(selectedClip.SourcePath, selectedClip.TrimStart, splitPoint, selectedClip.Rotation, selectedClip.Speed);
        var clipB = new VideoClip(selectedClip.SourcePath, splitPoint, selectedClip.TrimEnd, selectedClip.Rotation, selectedClip.Speed);

        _timelineClips.RemoveAt(originalIndex);
        _timelineClips.Insert(originalIndex, clipA);
        _timelineClips.Insert(originalIndex + 1, clipB);

        Console.WriteLine($"Clip '{selectedClip.DisplayName}' split into two clips at {splitPoint}.");
    }

    public async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_timelineClips.Count == 0)
        {
            Console.WriteLine("Please add at least one item to the timeline.");
            return;
        }

        var outputPath = "output.mp4";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "VideoEditor_Temp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        Console.WriteLine("Starting video export...");
        ExportButton.IsEnabled = false;

        try
        {
            // Determine target video properties from the first video clip
            var firstVideo = _timelineClips.OfType<VideoClip>().FirstOrDefault();
            string resolution = "1920x1080";
            string frameRate = "30";
            if (firstVideo != null)
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(firstVideo.SourcePath);
                var videoStream = mediaInfo.VideoStreams.First();
                resolution = videoStream.Resolution;
                frameRate = videoStream.Framerate.ToString(CultureInfo.InvariantCulture);
            }

            var tempClipPaths = new List<string>();
            for (int i = 0; i < _timelineClips.Count; i++)
            {
                var item = _timelineClips[i];
                var tempClipPath = Path.Combine(tempDirectory, $"{i}.mp4");
                Console.WriteLine($"Processing item {i + 1}/{_timelineClips.Count}: {item.DisplayName}");

                IConversion conversion;
                if (item is VideoClip videoClip)
                {
                    conversion = FFmpeg.Conversions.New()
                        .AddParameter($"-ss {videoClip.TrimStart.TotalSeconds}")
                        .AddParameter($"-to {videoClip.TrimEnd.TotalSeconds}")
                        .AddParameter($"-i \"{videoClip.SourcePath}\"");

                    var videoFilters = new StringBuilder();
                    if (videoClip.Rotation != VideoRotation.None)
                    {
                        switch (videoClip.Rotation)
                        {
                            case VideoRotation.Rotate90: videoFilters.Append("transpose=1"); break;
                            case VideoRotation.Rotate180: videoFilters.Append("transpose=2,transpose=2"); break;
                            case VideoRotation.Rotate270: videoFilters.Append("transpose=2"); break;
                        }
                    }

                    bool needsReEncoding = videoFilters.Length > 0 || videoClip.Speed != 1.0f;
                    if (needsReEncoding)
                    {
                        if (videoClip.Speed != 1.0f)
                        {
                            if (videoFilters.Length > 0) videoFilters.Append(',');
                            videoFilters.Append($"setpts={1.0f / videoClip.Speed:F4}*PTS");
                        }
                        conversion.AddParameter($"-vf \"{videoFilters}\"");
                        if (videoClip.Speed != 1.0f)
                        {
                            conversion.AddParameter($"-af atempo={videoClip.Speed.ToString(CultureInfo.InvariantCulture)}");
                        }
                    }
                    else
                    {
                        conversion.AddParameter("-c:v copy -c:a copy");
                    }
                }
                else if (item is ImageClip imageClip)
                {
                    conversion = FFmpeg.Conversions.New()
                        .AddParameter("-loop 1", true)
                        .AddParameter($"-i \"{imageClip.SourcePath}\"")
                        .AddParameter($"-t {imageClip.Duration.TotalSeconds}")
                        .AddParameter($"-s {resolution}")
                        .AddParameter($"-r {frameRate}")
                        .AddParameter("-c:v libx264")
                        .AddParameter("-pix_fmt yuv420p")
                        // Create a silent audio track to prevent concatenation issues
                        .AddParameter("-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=44100");
                }
                else
                {
                    continue; // Skip unknown item types
                }

                await conversion.SetOutput(tempClipPath).Start();
                tempClipPaths.Add(tempClipPath);
            }

            Console.WriteLine("All items processed. Concatenating...");
            if (tempClipPaths.Count > 1)
            {
                await FFmpeg.Conversions.FromSnippet.Concatenate(outputPath, tempClipPaths.ToArray());
            }
            else if (tempClipPaths.Count == 1)
            {
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
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
            ExportButton.IsEnabled = true;
        }
    }

    private void RotationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip || e.AddedItems.Count == 0)
            return;
        clip.Rotation = (VideoRotation)e.AddedItems[0]!;
    }

    private void SpeedUpDown_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip)
            return;
        clip.Speed = (float)e.NewValue;
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.SetRate(clip.Speed);
        }
    }
}
