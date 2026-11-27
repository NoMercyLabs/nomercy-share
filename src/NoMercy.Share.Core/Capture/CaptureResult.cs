namespace NoMercy.Share.Core.Capture;

/// <summary>A captured file on disk, ready to upload.</summary>
/// <param name="FilePath">The full path of the file.</param>
/// <param name="ContentType">The media type, for example image/png or video/mp4.</param>
/// <param name="Width">The picture width in pixels; 0 when not a picture or not known.</param>
/// <param name="Height">The picture height in pixels; 0 when not a picture or not known.</param>
/// <param name="SizeBytes">The file size in bytes; 0 when not known.</param>
/// <param name="SizeCapBytes">The size cap the file was squeezed to, in bytes; 0 when it has none.</param>
public sealed record CaptureResult(string FilePath, string ContentType, int Width = 0, int Height = 0, long SizeBytes = 0, long SizeCapBytes = 0)
{
    /// <summary>True when the file has a cap and is larger than it.</summary>
    public bool IsOverCap => SizeCapBytes > 0 && SizeBytes > SizeCapBytes;
}
