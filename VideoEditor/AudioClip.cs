using System;
using System.IO;

namespace VideoEditor;

public class AudioClip : ITimelineItem
{
    public string SourcePath { get; }
    public TimeSpan Duration { get; set; }
    public string DisplayName { get; }
    public double Volume { get; set; } = 1.0;

    public AudioClip(string sourcePath, TimeSpan duration)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source audio file was not found.", sourcePath);
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");
        }

        SourcePath = sourcePath;
        Duration = duration;
        DisplayName = Path.GetFileName(sourcePath);
    }
}
