namespace NoMercy.Share.Core.Capture;

/// <summary>What the squeeze pass produces: the picture size (both even) and the video bitrate in kilobits per second.</summary>
public readonly record struct VideoPlan(int Width, int Height, int VideoKbps);
