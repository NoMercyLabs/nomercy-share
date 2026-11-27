namespace NoMercy.Share.Core.Capture;

/// <summary>What the squeeze produces: an even picture size, the video and sound bitrates (0: no sound), and the frame rate (0: the recording's own).</summary>
public readonly record struct VideoPlan(int Width, int Height, int VideoKbps, int AudioKbps = 0, int FrameRate = 0);
