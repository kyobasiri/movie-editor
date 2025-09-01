using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VideoEditor;

public static class EnumConverters
{
    public static readonly IValueConverter VideoRotationToString =
        new FuncValueConverter<VideoRotation, string>(rotation =>
        {
            return rotation switch
            {
                VideoRotation.None => "None",
                VideoRotation.Rotate90 => "90°",
                VideoRotation.Rotate180 => "180°",
                VideoRotation.Rotate270 => "270°",
                _ => rotation.ToString()
            };
        });
}
