using System;

namespace VideoEditor;

public interface ITimelineItem
{
    /// <summary>
    /// Gets the duration of the item on the timeline.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets the user-friendly display name for the item.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the full path to the source media file.
    /// </summary>
    string SourcePath { get; }
}
