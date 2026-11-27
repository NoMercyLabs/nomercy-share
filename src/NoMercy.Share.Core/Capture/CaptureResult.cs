namespace NoMercy.Share.Core.Capture;

/// <summary>A captured file on disk, ready to upload.</summary>
/// <param name="FilePath">The full path of the file.</param>
/// <param name="ContentType">The media type, for example image/png or video/mp4.</param>
public sealed record CaptureResult(string FilePath, string ContentType);
