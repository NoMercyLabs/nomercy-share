using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>
/// Bamo's size math (BamoUtils, MIT) as one pure function: spread the size budget over the clip, shrink the picture until
/// the bits per pixel fit, and never spend more per pixel than the target.
/// </summary>
public static class VideoBudget
{
    /// <summary>The shortest duration the math divides by, so a clip of no length still gets a plan.</summary>
    private const double ShortestSeconds = 1;

    private const double KilobitsPerMegabyte = 8 * 1024;

    private const double BytesPerMegabyte = 1024 * 1024;

    private const double Epsilon = 1e-9;

    /// <summary>The picture size and video bitrate that keep a clip of this length under the size cap.</summary>
    public static VideoPlan Plan(double durationSeconds, int sourceWidth, int sourceHeight, RecordingSettings settings, bool includeAudio = false)
    {
        double seconds = Math.Max(durationSeconds, ShortestSeconds);
        double audioKbps = includeAudio ? settings.AudioBitrateKbps : 0;
        int sizeKbps = Math.Max(1, (int)((settings.MaxSizeMegabytes * settings.SizeHeadroom * KilobitsPerMegabyte / seconds) - audioKbps));

        double fit = settings.MaxWidth <= 0 || settings.MaxHeight <= 0
            ? 1 // the original size: no cap
            : Math.Min(1, Math.Min((double)settings.MaxWidth / sourceWidth, (double)settings.MaxHeight / sourceHeight));
        double affordablePixels = sizeKbps * 1000.0 / (settings.TargetBitsPerPixel * settings.FrameRate);
        double shrink = Math.Min(1, Math.Sqrt(affordablePixels / ((double)sourceWidth * fit * sourceHeight * fit)));
        int width = EvenFloor(sourceWidth * fit * shrink);
        int height = EvenFloor(sourceHeight * fit * shrink);

        int perPixelKbps = (int)(settings.TargetBitsPerPixel * width * height * settings.FrameRate / 1000);
        return new VideoPlan(width, height, Math.Max(1, Math.Min(sizeKbps, perPixelKbps)));
    }

    /// <summary>The plan for one more encode of a file that came out too big: the same picture, the bitrate times cap over actual size times the headroom.</summary>
    public static VideoPlan Retry(VideoPlan previous, long actualBytes, RecordingSettings settings)
    {
        double capBytes = settings.MaxSizeMegabytes * BytesPerMegabyte;
        return previous with { VideoKbps = Math.Max(1, (int)(previous.VideoKbps * (capBytes / actualBytes) * settings.SizeHeadroom)) };
    }

    /// <summary>The largest even whole number not above the value, at least 2.</summary>
    private static int EvenFloor(double value) => Math.Max(2, (int)Math.Floor((value + Epsilon) / 2) * 2);
}
