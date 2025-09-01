using System;

namespace VideoEditor;

public class TextOverlay
{
    public string Text { get; set; } = "Sample Text";
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan EndTime { get; set; } = TimeSpan.FromSeconds(5);
    public int X { get; set; } = 10;
    public int Y { get; set; } = 10;
    public int FontSize { get; set; } = 24;
    public string FontColor { get; set; } = "white";

    public double StartTimeSeconds
    {
        get => StartTime.TotalSeconds;
        set => StartTime = TimeSpan.FromSeconds(value);
    }

    public double EndTimeSeconds
    {
        get => EndTime.TotalSeconds;
        set => EndTime = TimeSpan.FromSeconds(value);
    }
    // In a real app, you'd want to specify a font file path.
    // For now, we'll rely on FFmpeg's default font.
    // public string FontFile { get; set; }
}
