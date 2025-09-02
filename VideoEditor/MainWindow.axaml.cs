using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
    private readonly ObservableCollection<ITimelineItem> _mediaLibrary = new();
    private readonly ObservableCollection<ITimelineItem> _timelineClips = new();
    private readonly ObservableCollection<AudioClip> _audioClips = new();
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

        MediaListBox.ItemsSource = _mediaLibrary;
        TimelineItemsControl.ItemsSource = _timelineClips;
        AudioTimelineItemsControl.ItemsSource = _audioClips;

        RotationComboBox.ItemsSource = Enum.GetValues(typeof(VideoRotation));
        FlipComboBox.ItemsSource = Enum.GetValues(typeof(VideoFlip));
        FilterComboBox.ItemsSource = Enum.GetValues(typeof(VideoFilterType));
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
                    _mediaLibrary.Add(new ImageClip(path, TimeSpan.FromSeconds(5)));
                }
                else
                {
                    var mediaInfo = await FFmpeg.GetMediaInfo(path);
                    var duration = mediaInfo.Duration;
                    if (duration <= TimeSpan.Zero)
                    {
                        Console.WriteLine($"[ERROR] Media file '{Path.GetFileName(path)}' has a duration of zero or less and will be skipped.");
                        continue;
                    }
                    _mediaLibrary.Add(new VideoClip(path, TimeSpan.Zero, duration));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting media info for {path}: {ex.Message}");
            }
        }
    }

    public async void AddAudioButton_Click(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Audio Files",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Audio Files") { Patterns = new[] { "*.mp3", "*.wav", "*.m4a", "*.flac" } },
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
                _audioClips.Add(new AudioClip(path, duration));
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
            ClipPropertiesPanel.IsVisible = false;
            VideoPropertiesPanel.IsVisible = false;
            ImageDurationPanel.IsVisible = false;
            OverlaysListBox.ItemsSource = null;

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ITimelineItem item)
            {
                ClipPropertiesPanel.IsVisible = true;
                if (item is VideoClip clip)
                {
                    VideoPropertiesPanel.IsVisible = true;
                    RotationComboBox.SelectedItem = clip.Rotation;
                    FlipComboBox.SelectedItem = clip.Flip;
                    FilterComboBox.SelectedItem = clip.Filter;
                    SpeedUpDown.Value = (decimal)clip.Speed;
                    VideoVolumeSlider.Value = clip.Volume;
                    OverlaysListBox.ItemsSource = clip.Overlays;

                    _currentMedia?.Dispose();
                    _currentMedia = new Media(_libVLC, new Uri(clip.SourcePath),
                        $":start-time={clip.TrimStart.TotalSeconds}",
                        $":stop-time={clip.TrimEnd.TotalSeconds}",
                        $":rate={clip.Speed}");
                    _mediaPlayer.Play(_currentMedia);
                }
                else if (item is ImageClip imageClip)
                {
                    ImageDurationPanel.IsVisible = true;
                    ImageDurationUpDown.Value = (decimal)imageClip.Duration.TotalSeconds;

                    _mediaPlayer.Stop();
                    _currentMedia?.Dispose();
                    _currentMedia = null;
                }
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

        var clipA = new VideoClip(selectedClip.SourcePath, selectedClip.TrimStart, splitPoint, selectedClip.Rotation, selectedClip.Speed, selectedClip.Flip, selectedClip.Volume, selectedClip.Filter);
        clipA.Overlays.AddRange(selectedClip.Overlays); // Copy overlays to the first part
        var clipB = new VideoClip(selectedClip.SourcePath, splitPoint, selectedClip.TrimEnd, selectedClip.Rotation, selectedClip.Speed, selectedClip.Flip, selectedClip.Volume, selectedClip.Filter);

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

        var finalOutputPath = "output.mp4";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "VideoEditor_Temp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        Console.WriteLine("Starting video export...");
        ExportButton.IsEnabled = false;

        try
        {
            // --- Step 1: Process and Concatenate Video/Image Clips ---
            Console.WriteLine("Step 1: Processing visual timeline...");
            var tempVideoPath = Path.Combine(tempDirectory, "temp_video_with_audio.mp4");

            var firstVideo = _timelineClips.OfType<VideoClip>().FirstOrDefault();
            string resolution = "1920x1080";
            string frameRate = "30";
            if (firstVideo != null)
            {
                var mediaInfo = await FFmpeg.GetMediaInfo(firstVideo.SourcePath);
                var videoStream = mediaInfo.VideoStreams.First();
                resolution = $"{videoStream.Width}x{videoStream.Height}";
                frameRate = videoStream.Framerate.ToString(CultureInfo.InvariantCulture);
            }

            var tempVisualClipPaths = new List<string>();
            for (int i = 0; i < _timelineClips.Count; i++)
            {
                var item = _timelineClips[i];
                var tempClipPath = Path.Combine(tempDirectory, $"visual_{i}.mp4");
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

                    if (videoClip.Flip != VideoFlip.None)
                    {
                        if (videoFilters.Length > 0) videoFilters.Append(',');
                        switch (videoClip.Flip)
                        {
                            case VideoFlip.Horizontal: videoFilters.Append("hflip"); break;
                            case VideoFlip.Vertical: videoFilters.Append("vflip"); break;
                        }
                    }

                    foreach (var overlay in videoClip.Overlays)
                    {
                        if (videoFilters.Length > 0) videoFilters.Append(',');
                        // Basic text escaping: wrap in single quotes. More complex text would need more robust escaping.
                        var escapedText = overlay.Text.Replace("'", "'\\''");
                        videoFilters.Append($"drawtext=text='{escapedText}':x={overlay.X}:y={overlay.Y}:fontsize={overlay.FontSize}:fontcolor={overlay.FontColor}:enable='between(t,{overlay.StartTime.TotalSeconds},{overlay.EndTime.TotalSeconds})'");
                    }

                    if (videoClip.Filter != VideoFilterType.None)
                    {
                        if (videoFilters.Length > 0) videoFilters.Append(',');
                        switch (videoClip.Filter)
                        {
                            case VideoFilterType.Sepia:
                                videoFilters.Append("colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131");
                                break;
                            case VideoFilterType.Monochrome:
                                videoFilters.Append("colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3");
                                break;
                            case VideoFilterType.Vintage:
                                videoFilters.Append("curves=r='0/0.11 .42/.51 1/0.95':g='0/0 .50/.48 1/1':b='0/0.22 .49/.44 1/0.8'");
                                break;
                        }
                    }

                    var audioFilters = new StringBuilder();
                    if (videoClip.Speed != 1.0f) audioFilters.Append($"atempo={videoClip.Speed.ToString(CultureInfo.InvariantCulture)}");
                    if (videoClip.Volume != 1.0)
                    {
                        if (audioFilters.Length > 0) audioFilters.Append(',');
                        audioFilters.Append($"volume={videoClip.Volume.ToString(CultureInfo.InvariantCulture)}");
                    }

                    bool needsVideoReEncoding = videoFilters.Length > 0 || videoClip.Speed != 1.0f;
                    bool needsAudioReEncoding = audioFilters.Length > 0;

                    if (needsVideoReEncoding)
                    {
                        if (videoClip.Speed != 1.0f)
                        {
                            if (videoFilters.Length > 0) videoFilters.Append(',');
                            videoFilters.Append($"setpts={1.0f / videoClip.Speed:F4}*PTS");
                        }
                        conversion.AddParameter($"-vf \"{videoFilters}\"");
                    }
                    else
                    {
                        conversion.AddParameter("-c:v copy");
                    }

                    if(needsAudioReEncoding)
                    {
                        conversion.AddParameter($"-af \"{audioFilters}\"");
                    }
                    else
                    {
                        conversion.AddParameter("-c:a copy");
                    }
                }
                else if (item is ImageClip imageClip)
                {
                    // ... (same as before)
                    conversion = FFmpeg.Conversions.New()
                        .AddParameter("-loop 1", ParameterPosition.PreInput)
                        .AddParameter($"-i \"{imageClip.SourcePath}\"")
                        .AddParameter($"-t {imageClip.Duration.TotalSeconds}")
                        .AddParameter($"-s {resolution}")
                        .AddParameter($"-r {frameRate}")
                        .AddParameter("-c:v libx264")
                        .AddParameter("-pix_fmt yuv420p")
                        .AddParameter("-f lavfi -i anullsrc=channel_layout=stereo:sample_rate=44100");
                }
                else continue;

                await conversion.SetOutput(tempClipPath).Start();
                tempVisualClipPaths.Add(tempClipPath);
            }

            Console.WriteLine("All visual items processed. Concatenating...");
            if (tempVisualClipPaths.Count > 1)
            {
                await FFmpeg.Conversions.FromSnippet.Concatenate(tempVideoPath, tempVisualClipPaths.ToArray());
            }
            else if (tempVisualClipPaths.Count == 1)
            {
                File.Move(tempVisualClipPaths.First(), tempVideoPath, true);
            }

            // --- Step 2: Check for Audio Clips and Mix ---
            if (_audioClips.Count == 0)
            {
                Console.WriteLine("No audio clips to mix. Finalizing video.");
                File.Move(tempVideoPath, finalOutputPath, true);
            }
            else
            {
                Console.WriteLine("Step 2: Processing and mixing audio track...");
                var tempAudioClipPaths = new List<string>();
                for(int i = 0; i < _audioClips.Count; i++)
                {
                    var audioClip = _audioClips[i];
                    var tempAudioPath = Path.Combine(tempDirectory, $"audio_{i}.m4a");
                    Console.WriteLine($"Processing audio clip {i+1}/{_audioClips.Count}: {audioClip.DisplayName}");

                    var audioConversion = FFmpeg.Conversions.New()
                        .AddParameter($"-i \"{audioClip.SourcePath}\"");

                    if(audioClip.Volume != 1.0)
                    {
                        audioConversion.AddParameter($"-af \"volume={audioClip.Volume.ToString(CultureInfo.InvariantCulture)}\"");
                    }
                    else
                    {
                        audioConversion.AddParameter("-c copy");
                    }
                    await audioConversion.SetOutput(tempAudioPath).Start();
                    tempAudioClipPaths.Add(tempAudioPath);
                }

                var tempAudioConcatPath = Path.Combine(tempDirectory, "concat_audio_list.txt");
                var tempFullAudioPath = Path.Combine(tempDirectory, "temp_full_audio.m4a");
                var audioFileNames = tempAudioClipPaths.Select(path => $"file '{path}'").ToList();
                await File.WriteAllLinesAsync(tempAudioConcatPath, audioFileNames);

                await FFmpeg.Conversions.New()
                    .AddParameter("-f concat -safe 0")
                    .AddParameter($"-i \"{tempAudioConcatPath}\"")
                    .AddParameter("-c copy")
                    .SetOutput(tempFullAudioPath)
                    .Start();

                Console.WriteLine("Mixing video and audio tracks...");
                // Final mixing logic remains the same
                await FFmpeg.Conversions.New()
                    .AddParameter($"-i \"{tempVideoPath}\"")
                    .AddParameter($"-i \"{tempFullAudioPath}\"")
                    .AddParameter("-filter_complex \"[0:a][1:a]amix=inputs=2:duration=longest[a]\"")
                    .AddParameter("-map 0:v")
                    .AddParameter("-map \"[a]\"")
                    .AddParameter("-c:v copy")
                    .SetOutput(finalOutputPath)
                    .Start();
            }

            Console.WriteLine($"Export finished! Video saved to: {finalOutputPath}");
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
        if (e.AddedItems[0] is VideoRotation rotation)
        {
            clip.Rotation = rotation;
        }
    }

    private void FlipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip || e.AddedItems.Count == 0)
            return;
        if (e.AddedItems[0] is VideoFlip flip)
        {
            clip.Flip = flip;
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip || e.AddedItems.Count == 0)
            return;
        if (e.AddedItems[0] is VideoFilterType filter)
        {
            clip.Filter = filter;
        }
    }

    private void SpeedUpDown_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip || !e.NewValue.HasValue)
            return;
        clip.Speed = (float)e.NewValue.Value;
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.SetRate(clip.Speed);
        }
    }

    private void VideoVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not VideoClip clip)
            return;
        clip.Volume = e.NewValue;
    }

    private void ImageDurationUpDown_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isUpdatingClipProperties || MediaListBox.SelectedItem is not ImageClip clip || !e.NewValue.HasValue)
            return;
        clip.Duration = TimeSpan.FromSeconds((double)e.NewValue.Value);
    }

    private async void AddOverlayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is not VideoClip selectedClip)
        {
            Console.WriteLine("Please select a video clip first.");
            return;
        }

        var newOverlay = new TextOverlay();
        var editor = new TextOverlayEditorWindow(newOverlay);
        var result = await editor.ShowDialog<bool>(this);

        if (result)
        {
            selectedClip.Overlays.Add(newOverlay);
            // Refresh the ListBox
            OverlaysListBox.ItemsSource = null;
            OverlaysListBox.ItemsSource = selectedClip.Overlays;
        }
    }

    private async void EditOverlayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is not VideoClip selectedClip || OverlaysListBox.SelectedItem is not TextOverlay selectedOverlay)
        {
            Console.WriteLine("Please select a video clip and a text overlay to edit.");
            return;
        }

        var editor = new TextOverlayEditorWindow(selectedOverlay);
        var result = await editor.ShowDialog<bool>(this);

        if (result)
        {
            // Object is edited by reference, just need to refresh UI
            OverlaysListBox.ItemsSource = null;
            OverlaysListBox.ItemsSource = selectedClip.Overlays;
        }
    }

    private void RemoveOverlayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (MediaListBox.SelectedItem is not VideoClip selectedClip || OverlaysListBox.SelectedItem is not TextOverlay selectedOverlay)
        {
            Console.WriteLine("Please select a video clip and a text overlay to remove.");
            return;
        }

        selectedClip.Overlays.Remove(selectedOverlay);
        // Refresh the ListBox
        OverlaysListBox.ItemsSource = null;
        OverlaysListBox.ItemsSource = selectedClip.Overlays;
    }

    private async void MediaLibrary_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: ITimelineItem item }) return;

        var data = new DataObject();
        data.Set("MediaLibraryItem", item);

        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

    private async void TimelineItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: ITimelineItem item }) return;

        var data = new DataObject();
        data.Set("TimelineItem", item);

        var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

        if (result == DragDropEffects.Move)
        {
            // The drop handler will have already moved the item.
        }
    }

    private void Timeline_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects &= (DragDropEffects.Move | DragDropEffects.Copy);
        if (!e.Data.Contains("TimelineItem") && !e.Data.Contains("MediaLibraryItem"))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Timeline_Drop(object? sender, DragEventArgs e)
    {
        var targetControl = e.Source as Control;
        if (targetControl == null) return;
        var itemsControl = sender as ItemsControl;
        if (itemsControl == null) return;

        // Find the item we are dropping on top of.
        Control? targetItemContainer = null;
        var current = targetControl;
        while (current != null)
        {
            var parent = current.Parent as Control;
            if (parent == itemsControl.ItemsPanelRoot) { targetItemContainer = current; break; }
            if (parent == null || parent == itemsControl) { break; }
            current = parent;
        }
        int newIndex = -1;
        if (targetItemContainer?.DataContext is ITimelineItem targetItem)
        {
            newIndex = _timelineClips.IndexOf(targetItem);
        }
        else
        {
            newIndex = _timelineClips.Count;
        }
        if (newIndex < 0) { newIndex = _timelineClips.Count; }


        // Handle drop from Media Library (add new item)
        if (e.Data.Get("MediaLibraryItem") is ITimelineItem mediaItem)
        {
            ITimelineItem newItem;
            if (mediaItem is VideoClip videoClip)
            {
                newItem = videoClip.CloneWithNewTimes(videoClip.TrimStart, videoClip.TrimEnd);
            }
            else if (mediaItem is ImageClip imageClip)
            {
                // Assuming ImageClip has a constructor to create a copy
                newItem = new ImageClip(imageClip.SourcePath, imageClip.Duration);
            }
            else
            {
                return; // Should not happen
            }
            _timelineClips.Insert(newIndex, newItem);
        }
        // Handle drop from Timeline (re-order existing item)
        else if (e.Data.Get("TimelineItem") is ITimelineItem timelineItem)
        {
            var oldIndex = _timelineClips.IndexOf(timelineItem);
            if (oldIndex < 0) return;

            // Adjust newIndex if moving an item from left to right in the list
            if (oldIndex < newIndex)
            {
                newIndex--;
            }

            if (oldIndex != newIndex)
            {
                _timelineClips.Move(oldIndex, newIndex);
            }
        }
    }
}
