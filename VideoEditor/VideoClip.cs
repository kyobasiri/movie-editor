using System;
using System.Collections.Generic;
using System.IO;

namespace VideoEditor;

public enum VideoRotation
{
    None,
    Rotate90,
    Rotate180,
    Rotate270
}

public enum VideoFlip
{
    None,
    Horizontal,
    Vertical
}

public enum VideoFilterType
{
    None,
    Sepia,
    Monochrome,
    Vintage
}

public class VideoClip : ITimelineItem
{
    /// <summary>
    /// Gets the full path to the source media file.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the time in the source media file where this clip begins.
    /// </summary>
    public TimeSpan TrimStart { get; set; }

    /// <summary>
    /// Gets the time in the source media file where this clip ends.
    /// </summary>
    public TimeSpan TrimEnd { get; set; }

    /// <summary>
    /// Gets or sets the rotation to be applied to the video clip.
    /// </summary>
    public VideoRotation Rotation { get; set; } = VideoRotation.None;

    /// <summary>
    /// Gets or sets the flip to be applied to the video clip.
    /// </summary>
    public VideoFlip Flip { get; set; } = VideoFlip.None;

    /// <summary>
    /// Gets or sets the playback speed of the clip (1.0 is normal speed).
    /// </summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the volume of the clip's audio (0.0 to 1.0).
    /// </summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the list of text overlays for this clip.
    /// </summary>
    public List<TextOverlay> Overlays { get; set; } = new();

    /// <summary>
    /// Gets or sets the visual filter to be applied to the video clip.
    /// </summary>
    public VideoFilterType Filter { get; set; } = VideoFilterType.None;

    /// <summary>
    /// Gets the duration of this clip, adjusted for speed.
    /// </summary>
    public TimeSpan Duration => (TrimEnd - TrimStart) / Speed;

    /// <summary>
    /// Gets a user-friendly display name for the clip.
    /// </summary>
    public string DisplayName { get; }

    public VideoClip(string sourcePath, TimeSpan trimStart, TimeSpan trimEnd, VideoRotation rotation = VideoRotation.None, float speed = 1.0f, VideoFlip flip = VideoFlip.None, double volume = 1.0, VideoFilterType filter = VideoFilterType.None)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source video file was not found.", sourcePath);
        }

        if (trimStart < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(trimStart), "TrimStart cannot be negative.");
        }

        if (trimEnd <= trimStart)
        {
            throw new ArgumentException("TrimEnd must be greater than TrimStart.", nameof(trimEnd));
        }

        if (speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be positive.");
        }

        SourcePath = sourcePath;
        TrimStart = trimStart;
        TrimEnd = trimEnd;
        Rotation = rotation;
        Speed = speed;
        Flip = flip;
        Volume = volume;
        Filter = filter;
        DisplayName = Path.GetFileName(sourcePath);
    }

    /// <summary>
    /// Clones the current video clip with a new start and end time.
    /// </summary>
    public VideoClip CloneWithNewTimes(TimeSpan newTrimStart, TimeSpan newTrimEnd)
    {
        var newClip = new VideoClip(this.SourcePath, newTrimStart, newTrimEnd, this.Rotation, this.Speed, this.Flip, this.Volume, this.Filter);
        // Perform a shallow copy of the overlays. For a more robust solution, deep cloning might be needed.
        newClip.Overlays.AddRange(this.Overlays);
        return newClip;
    }
}
