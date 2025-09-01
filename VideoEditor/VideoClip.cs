using System;
using System.IO;

namespace VideoEditor;

public enum VideoRotation
{
    None,
    Rotate90,
    Rotate180,
    Rotate270
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
    /// Gets or sets the playback speed of the clip (1.0 is normal speed).
    /// </summary>
    public float Speed { get; set; } = 1.0f;

    /// <summary>
    /// Gets the duration of this clip, adjusted for speed.
    /// </summary>
    public TimeSpan Duration => (TrimEnd - TrimStart) / Speed;

    /// <summary>
    /// Gets a user-friendly display name for the clip.
    /// </summary>
    public string DisplayName { get; }

    public VideoClip(string sourcePath, TimeSpan trimStart, TimeSpan trimEnd, VideoRotation rotation = VideoRotation.None, float speed = 1.0f)
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
        DisplayName = Path.GetFileName(sourcePath);
    }

    /// <summary>
    /// Clones the current video clip with a new start and end time.
    /// </summary>
    public VideoClip CloneWithNewTimes(TimeSpan newTrimStart, TimeSpan newTrimEnd)
    {
        return new VideoClip(this.SourcePath, newTrimStart, newTrimEnd, this.Rotation, this.Speed);
    }
}
