using System;
using System.IO;

namespace VideoEditor;

public class ImageClip : ITimelineItem
{
    public string SourcePath { get; }
    public TimeSpan Duration { get; set; }
    public string DisplayName { get; }

    public ImageClip(string sourcePath, TimeSpan duration)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The source image file was not found.", sourcePath);
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
