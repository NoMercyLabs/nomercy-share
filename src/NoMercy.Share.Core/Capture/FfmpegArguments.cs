using System.Globalization;

namespace NoMercy.Share.Core.Capture;

/// <summary>The ffmpeg argument lists of a recording. They go to the process as a list, so a path with spaces or quotes stays one argument.</summary>
public static class FfmpegArguments
{
    private const string RecordPreset = "ultrafast";

    private const string RecordQuality = "18";

    private const string SqueezePreset = "veryfast";

    private const string QuietLog = "warning";

    /// <summary>Records one monitor region with Desktop Duplication into a quick, near-lossless file. ffmpeg stops cleanly when it reads "q" on its input.</summary>
    public static IReadOnlyList<string> Record(RecordingTarget target, int frameRate, string outputPath) =>
    [
        "-hide_banner", "-loglevel", QuietLog, "-nostats",
        "-f", "lavfi", "-i", string.Create(CultureInfo.InvariantCulture,
            $"ddagrab=output_idx={target.MonitorIndex}:framerate={frameRate}:draw_mouse=1:offset_x={target.OffsetX}:offset_y={target.OffsetY}:video_size={target.Width}x{target.Height}"),
        "-vf", "hwdownload,format=bgra",
        "-c:v", "libx264", "-preset", RecordPreset, "-crf", RecordQuality,
        "-y", outputPath
    ];

    /// <summary>Squeezes the recording to the plan in one pass: the bitrate is held to the budget, and the file starts playing before it has downloaded.</summary>
    public static IReadOnlyList<string> Compress(string inputPath, string outputPath, VideoPlan plan)
    {
        string rate = string.Create(CultureInfo.InvariantCulture, $"{plan.VideoKbps}k");
        string buffer = string.Create(CultureInfo.InvariantCulture, $"{plan.VideoKbps * 2L}k");
        return
        [
            "-hide_banner", "-loglevel", QuietLog, "-nostats", "-nostdin",
            "-i", inputPath,
            "-vf", string.Create(CultureInfo.InvariantCulture, $"scale={plan.Width}:{plan.Height}:flags=lanczos"),
            "-c:v", "libx264", "-preset", SqueezePreset,
            "-b:v", rate, "-maxrate", rate, "-bufsize", buffer,
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-an",
            "-y", outputPath
        ];
    }
}
