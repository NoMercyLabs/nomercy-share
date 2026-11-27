namespace NoMercy.Share.Core.Capture;

/// <summary>One recorded sound file and how many seconds after the first video frame its first sample was taken.</summary>
public readonly record struct AudioTrack(string Path, double OffsetSeconds);
