using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Bamo's size math (BamoUtils, MIT): spread the budget over the clip, give the sound a share that fits, shrink the
/// picture until the bits per pixel fit. Added here: before the picture gets too small to read, the frame rate drops.</summary>
public static class VideoBudget
{
    /// <summary>The shortest duration the math divides by, so a clip of no length still gets a plan.</summary>
    private const double ShortestSeconds = 1;

    private const double KilobitsPerMegabyte = 8 * 1024;

    private const double BytesPerMegabyte = 1024 * 1024;

    private const double Epsilon = 1e-9;

    /// <summary>The picture size, frame rate and bitrates that keep a clip of this length under the size cap.</summary>
    public static VideoPlan Plan(double durationSeconds, int sourceWidth, int sourceHeight, RecordingSettings settings, bool includeAudio = false)
    {
        double seconds = Math.Max(durationSeconds, ShortestSeconds);
        int audioKbps = includeAudio ? AudioKbps(settings.MaxSizeMegabytes * KilobitsPerMegabyte / seconds, settings) : 0;
        int sizeKbps = Math.Max(1, (int)((settings.MaxSizeMegabytes * settings.SizeHeadroom * KilobitsPerMegabyte / seconds) - audioKbps));

        double fit = settings.MaxWidth <= 0 || settings.MaxHeight <= 0
            ? 1 // the original size: no cap
            : Math.Min(1, Math.Min((double)settings.MaxWidth / sourceWidth, (double)settings.MaxHeight / sourceHeight));
        (int width, int height) = Picture(sourceWidth * fit, sourceHeight * fit, sizeKbps, settings.TargetBitsPerPixel, settings.FrameRate);
        int frameRate = settings.FrameRate;
        if (height < settings.LowBudgetMinHeight && settings.LowBudgetFrameRate < settings.FrameRate)
        {
            frameRate = settings.LowBudgetFrameRate;
            (width, height) = Picture(sourceWidth * fit, sourceHeight * fit, sizeKbps, settings.TargetBitsPerPixel, frameRate);
        }

        int perPixelKbps = (int)(settings.TargetBitsPerPixel * width * height * frameRate / 1000);
        return new VideoPlan(width, height, Math.Max(1, Math.Min(sizeKbps, perPixelKbps)), audioKbps, frameRate == settings.FrameRate ? 0 : frameRate);
    }

    /// <summary>The plan for one more encode of a file that came out too big: the same picture, the bitrate times cap over actual size times the headroom.</summary>
    public static VideoPlan Retry(VideoPlan previous, long actualBytes, RecordingSettings settings)
    {
        double capBytes = settings.MaxSizeMegabytes * BytesPerMegabyte;
        return previous with { VideoKbps = Math.Max(1, (int)(previous.VideoKbps * (capBytes / actualBytes) * settings.SizeHeadroom)) };
    }

    /// <summary>The largest picture of this shape whose pixels the bitrate can pay for at the target bits per pixel.</summary>
    private static (int Width, int Height) Picture(double width, double height, int kbps, double bitsPerPixel, int frameRate)
    {
        double affordablePixels = kbps * 1000.0 / (bitsPerPixel * frameRate);
        double shrink = Math.Min(1, Math.Sqrt(affordablePixels / (width * height)));
        return (EvenFloor(width * shrink), EvenFloor(height * shrink));
    }

    /// <summary>Bamo's sound steps by the total budget, never above the setting.</summary>
    private static int AudioKbps(double totalKbps, RecordingSettings settings) =>
        Math.Min(settings.AudioBitrateKbps, ShareDefaults.RecordingAudioSteps.First(step => totalKbps >= step.TotalKbps).AudioKbps);

    /// <summary>The largest even whole number not above the value, at least 2.</summary>
    private static int EvenFloor(double value) => Math.Max(2, (int)Math.Floor((value + Epsilon) / 2) * 2);
}
