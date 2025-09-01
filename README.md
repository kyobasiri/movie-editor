# VideoEditor MVP

This is an MVP (Minimum Viable Product) for a video editor application built with .NET 8 and Avalonia UI.

## Features

*   **Media Loading:** Add multiple video files to a media list.
*   **Video Preview:** Select a video from the list to play it in the preview pane. (Powered by LibVLCSharp)
*   **Video Concatenation:** Export all videos in the media list into a single `output.mp4` file. (Powered by FFmpeg)

## How to Build

1.  Ensure you have the .NET 8 SDK installed.
2.  Run the following command in the root directory:
    ```bash
    dotnet build
    ```

## Runtime Dependencies

### Linux

This application uses `LibVLCSharp` for video playback, which depends on a system-wide installation of LibVLC.

On Debian-based distributions (like Ubuntu), you can install the necessary packages with:

```bash
sudo apt-get update
sudo apt-get install -y libvlc-dev libvlc5
```

### Windows & macOS

The necessary native libraries for VLC are bundled with the application via NuGet packages and should work out of the box.
