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
    public static IReadOnlyList<string> Record(RecordingTarget target, int frameRate, string outputPath, bool flushEachPacket = false) =>
    [
        "-hide_banner", "-loglevel", QuietLog, "-nostats",
        "-f", "lavfi", "-i", string.Create(CultureInfo.InvariantCulture,
            $"ddagrab=output_idx={target.MonitorIndex}:framerate={frameRate}:draw_mouse=1:offset_x={target.OffsetX}:offset_y={target.OffsetY}:video_size={target.Width}x{target.Height}"),
        "-vf", "hwdownload,format=bgra",
        "-c:v", "libx264", "-preset", RecordPreset, "-crf", RecordQuality,
        .. flushEachPacket ? (string[])["-flush_packets", "1"] : [],
        "-y", outputPath
    ];

    /// <summary>Squeezes the recording to the plan in one pass: the bitrate is held to the budget, and the file starts playing before it has downloaded. Sound files are extra inputs, mixed into one AAC stream.</summary>
    public static IReadOnlyList<string> Compress(string inputPath, string outputPath, VideoPlan plan, IReadOnlyList<AudioTrack>? sound = null, int audioKbps = 0)
    {
        sound ??= [];
        string rate = string.Create(CultureInfo.InvariantCulture, $"{plan.VideoKbps}k");
        string buffer = string.Create(CultureInfo.InvariantCulture, $"{plan.VideoKbps * 2L}k");
        List<string> args = ["-hide_banner", "-loglevel", QuietLog, "-nostats", "-nostdin", "-i", inputPath];
        foreach (AudioTrack track in sound)
        {
            args.AddRange(["-itsoffset", string.Create(CultureInfo.InvariantCulture, $"{track.OffsetSeconds:F3}"), "-i", track.Path]);
        }

        args.AddRange(["-vf", string.Create(CultureInfo.InvariantCulture, $"scale={plan.Width}:{plan.Height}:flags=lanczos")]);
        if (sound.Count > 1)
        {
            args.AddRange(["-filter_complex", Mix(sound.Count)]);
        }

        args.AddRange(
        [
            "-c:v", "libx264", "-preset", SqueezePreset,
            "-b:v", rate, "-maxrate", rate, "-bufsize", buffer,
            "-pix_fmt", "yuv420p", "-movflags", "+faststart"
        ]);
        args.AddRange(sound.Count switch
        {
            0 => ["-an"],
            1 => ["-map", "0:v:0", "-map", "1:a:0", "-c:a", "aac", "-b:a", Kilobits(audioKbps)],
            _ => ["-map", "0:v:0", "-map", "[mix]", "-c:a", "aac", "-b:a", Kilobits(audioKbps)]
        });
        args.AddRange(["-y", outputPath]);
        return args;
    }

    /// <summary>The filter that pads each sound file to start at time zero and mixes them at full level; the first file sets the length.</summary>
    private static string Mix(int count)
    {
        IEnumerable<string> padded = Enumerable.Range(0, count)
            .Select(i => string.Create(CultureInfo.InvariantCulture, $"[{i + 1}:a]aresample=async=1:first_pts=0[s{i}]"));
        string labels = string.Concat(Enumerable.Range(0, count).Select(i => string.Create(CultureInfo.InvariantCulture, $"[s{i}]")));
        return string.Create(CultureInfo.InvariantCulture,
            $"{string.Join(';', padded)};{labels}amix=inputs={count}:duration=first:dropout_transition=0:normalize=0[mix]");
    }

    private static string Kilobits(int kbps) => string.Create(CultureInfo.InvariantCulture, $"{kbps}k");
}
