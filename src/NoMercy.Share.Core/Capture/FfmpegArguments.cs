using System.Globalization;

namespace NoMercy.Share.Core.Capture;

/// <summary>The ffmpeg argument lists of a recording. They go to the process as a list, so a path with spaces or quotes stays one argument.</summary>
public static class FfmpegArguments
{
    private const string RecordPreset = "ultrafast";

    private const string RecordQuality = "18";

    private const string QuietLog = "warning";

    /// <summary>The test picture of a probe: small, black, a few frames; enough for an encoder to open its hardware.</summary>
    private const string ProbeSource = "color=c=black:s=256x144:r=30";

    private const string ProbeFrames = "5";

    private const string ProbeRate = "500k";

    /// <summary>Records one monitor region with Desktop Duplication into a quick, near-lossless file. ffmpeg stops cleanly when it reads "q" on its input.</summary>
    public static IReadOnlyList<string> Record(RecordingTarget target, int frameRate, string outputPath, bool flushEachPacket = false) =>
        [.. DesktopDuplication(target, frameRate), .. RecordOutput(outputPath, flushEachPacket)];

    /// <summary>One frame of the same input into nothing: it exits with 0 only where Desktop Duplication can start.</summary>
    public static IReadOnlyList<string> ProbeDesktopDuplication(RecordingTarget target, int frameRate) =>
        [.. DesktopDuplication(target, frameRate), "-frames:v", "1", "-f", "null", "-"];

    private static string[] DesktopDuplication(RecordingTarget target, int frameRate) =>
    [
        "-hide_banner", "-loglevel", QuietLog, "-nostats",
        "-f", "lavfi", "-i", string.Create(CultureInfo.InvariantCulture,
            $"ddagrab=output_idx={target.MonitorIndex}:framerate={frameRate}:draw_mouse=1:offset_x={target.OffsetX}:offset_y={target.OffsetY}:video_size={target.Width}x{target.Height}"),
        "-vf", "hwdownload,format=bgra"
    ];

    /// <summary>The same pass with the GDI grabber, for where Desktop Duplication cannot start, as over Remote Desktop.</summary>
    public static IReadOnlyList<string> RecordWithGdi(ScreenRect area, int frameRate, string outputPath, bool flushEachPacket = false) =>
    [
        "-hide_banner", "-loglevel", QuietLog, "-nostats",
        "-f", "gdigrab", "-framerate", frameRate.ToString(CultureInfo.InvariantCulture), "-draw_mouse", "1",
        "-offset_x", area.X.ToString(CultureInfo.InvariantCulture), "-offset_y", area.Y.ToString(CultureInfo.InvariantCulture),
        "-video_size", string.Create(CultureInfo.InvariantCulture, $"{area.Width}x{area.Height}"), "-i", "desktop",
        .. RecordOutput(outputPath, flushEachPacket)
    ];

    private static string[] RecordOutput(string outputPath, bool flushEachPacket) =>
    [
        "-c:v", "libx264", "-preset", RecordPreset, "-crf", RecordQuality,
        .. flushEachPacket ? (string[])["-flush_packets", "1"] : [],
        "-y", outputPath
    ];

    /// <summary>Squeezes the recording to the plan: the bitrate held to the budget, sound files mixed into one stream. A two-pass
    /// encoder runs it twice: pass 1 only studies the picture and writes no file, pass 2 writes it.</summary>
    public static IReadOnlyList<string> Compress(string inputPath, string outputPath, VideoPlan plan, IReadOnlyList<AudioTrack>? sound = null, int audioKbps = 0,
        VideoEncoder? encoder = null, EncodePass? pass = null)
    {
        encoder ??= VideoEncoder.From(new Settings.RecordingSettings());
        bool studyOnly = pass is { Number: 1 };
        sound = studyOnly ? [] : sound ?? [];
        string rate = Kilobits(plan.VideoKbps);
        string buffer = Kilobits(plan.VideoKbps * 2);
        List<string> args = ["-hide_banner", "-loglevel", QuietLog, "-nostats", "-nostdin", "-i", inputPath];
        foreach (AudioTrack track in sound)
        {
            args.AddRange(["-itsoffset", string.Create(CultureInfo.InvariantCulture, $"{track.OffsetSeconds:F3}"), "-i", track.Path]);
        }

        args.AddRange(["-vf", string.Create(CultureInfo.InvariantCulture, $"scale={plan.Width}:{plan.Height}:flags=lanczos")]);
        if (plan.FrameRate > 0)
        {
            args.AddRange(["-r", plan.FrameRate.ToString(CultureInfo.InvariantCulture)]);
        }

        if (sound.Count > 1)
        {
            args.AddRange(["-filter_complex", Mix(sound.Count)]);
        }

        args.AddRange(encoder.Arguments());
        args.AddRange(RateArguments(encoder, rate, buffer));
        args.AddRange(["-pix_fmt", encoder.PixelFormat]);
        if (pass is { } twoPass)
        {
            args.AddRange(["-pass", twoPass.Number.ToString(CultureInfo.InvariantCulture), "-passlogfile", twoPass.LogPath]);
        }

        if (studyOnly)
        {
            args.AddRange(["-an", "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"]);
            return args;
        }

        if (encoder.Extension == ".mp4")
        {
            args.AddRange(["-movflags", "+faststart"]); // the file starts playing before it has downloaded
        }

        string sound_rate = Kilobits(plan.AudioKbps > 0 ? plan.AudioKbps : audioKbps);
        args.AddRange(sound.Count switch
        {
            0 => ["-an"],
            1 => ["-map", "0:v:0", "-map", "1:a:0", "-c:a", encoder.AudioCodec, "-b:a", sound_rate],
            _ => ["-map", "0:v:0", "-map", "[mix]", "-c:a", encoder.AudioCodec, "-b:a", sound_rate]
        });
        args.AddRange(["-y", outputPath]);
        return args;
    }

    /// <summary>Encodes a few black frames to nothing with the encoder and the same options a squeeze uses; exit 0 means it works on this PC.</summary>
    public static IReadOnlyList<string> Probe(VideoEncoder encoder) =>
    [
        "-hide_banner", "-loglevel", "error", "-nostats", "-nostdin",
        "-f", "lavfi", "-i", ProbeSource, "-frames:v", ProbeFrames,
        .. encoder.Arguments(), .. RateArguments(encoder, ProbeRate, ProbeRate),
        "-pix_fmt", encoder.PixelFormat,
        "-f", "null", "-"
    ];

    /// <summary>The bitrate held to the budget; the peak only where the encoder takes one.</summary>
    private static string[] RateArguments(VideoEncoder encoder, string rate, string buffer) =>
        encoder.TakesPeakRate ? ["-b:v", rate, "-maxrate", rate, "-bufsize", buffer] : ["-b:v", rate];

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
