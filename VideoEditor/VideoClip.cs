using System;
using System.IO;

namespace VideoEditor;

public class VideoClip
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
    /// Gets the duration of this clip.
    /// </summary>
    public TimeSpan Duration => TrimEnd - TrimStart;

    /// <summary>
    /// Gets a user-friendly display name for the clip.
    /// </summary>
    public string DisplayName { get; }

    public VideoClip(string sourcePath, TimeSpan trimStart, TimeSpan trimEnd)
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

        SourcePath = sourcePath;
        TrimStart = trimStart;
        TrimEnd = trimEnd;
        DisplayName = Path.GetFileName(sourcePath);
    }

    /// <summary>
    /// Clones the current video clip with a new start and end time.
    /// </summary>
    public VideoClip CloneWithNewTimes(TimeSpan newTrimStart, TimeSpan newTrimEnd)
    {
        return new VideoClip(this.SourcePath, newTrimStart, newTrimEnd);
    }
}
