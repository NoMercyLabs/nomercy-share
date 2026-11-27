namespace NoMercy.Share.Core.Capture;

/// <summary>A captured file on disk, ready to upload.</summary>
/// <param name="FilePath">The full path of the file.</param>
/// <param name="ContentType">The media type, for example image/png or video/mp4.</param>
/// <param name="Width">The picture width in pixels; 0 when not a picture or not known.</param>
/// <param name="Height">The picture height in pixels; 0 when not a picture or not known.</param>
public sealed record CaptureResult(string FilePath, string ContentType, int Width = 0, int Height = 0);
