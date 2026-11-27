using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>
/// The encoder of the shared file: a codec ("h264", "hevc", "av1"), a family that runs it ("cpu", "nvidia", "intel", "amd")
/// and a speed ("fast", "balanced", "quality"). This is the one place that knows each encoder's ffmpeg name and options.
/// </summary>
public sealed record VideoEncoder(string Codec, string Family, string Speed)
{
    public const string Cpu = "cpu";

    /// <summary>vp9 is Bamo's Discord encode: two passes into WebM, the smallest file for a hard size cap.</summary>
    public static readonly IReadOnlyList<string> Codecs = ["h264", "hevc", "av1", "vp9"];

    /// <summary>The CPU first: it works on every PC.</summary>
    public static readonly IReadOnlyList<string> Families = [Cpu, "nvidia", "intel", "amd"];

    public static readonly IReadOnlyList<string> Speeds = ["fast", "balanced", "quality"];

    /// <summary>The settings' choice; a value the app does not know falls back to the default.</summary>
    public static VideoEncoder From(RecordingSettings settings) => new(
        Codecs.Contains(settings.Codec) ? settings.Codec : ShareDefaults.RecordingCodec,
        Families.Contains(settings.Encoder) ? settings.Encoder : ShareDefaults.RecordingEncoder,
        Speeds.Contains(settings.Speed) ? settings.Speed : ShareDefaults.RecordingSpeed);

    public bool IsCpu => Family == Cpu;

    /// <summary>The same codec and speed on the CPU: the stand-in when a GPU encode fails.</summary>
    public VideoEncoder OnCpu() => this with { Family = Cpu };

    /// <summary>The ffmpeg encoder name, for example libx264 or hevc_nvenc.</summary>
    public string Name => (Family, Codec) switch
    {
        (Cpu, "hevc") => "libx265",
        (Cpu, "av1") => "libsvtav1",
        (Cpu, "vp9") => "libvpx-vp9",
        (Cpu, _) => "libx264",
        ("nvidia", _) => $"{Codec}_nvenc",
        ("intel", _) => $"{Codec}_qsv",
        _ => $"{Codec}_amf"
    };

    /// <summary>The encoder name, its speed option and the codec tag, as ffmpeg arguments.</summary>
    public IReadOnlyList<string> Arguments() =>
    [
        "-c:v", Name, .. SpeedOption(),
        .. Codec == "hevc" ? (string[])["-tag:v", "hvc1"] : [] // without it Apple players and Safari refuse HEVC in mp4
    ];

    /// <summary>Whether the encoder takes a peak rate. SVT-AV1 reads a peak equal to the rate as CBR (low delay only); two-pass VP9 spends its own peaks.</summary>
    public bool TakesPeakRate => Name is not ("libsvtav1" or "libvpx-vp9");

    /// <summary>libvpx VP9 studies the whole clip in pass 1, then spends the bits where pass 2 needs them: the most picture for a hard cap.</summary>
    public bool IsTwoPass => Name == "libvpx-vp9";

    private bool IsWebm => Codec == "vp9";

    public string Extension => IsWebm ? ".webm" : ".mp4";

    public string ContentType => IsWebm ? "video/webm" : "video/mp4";

    /// <summary>WebM carries Opus, which sounds good at 64 kbps; MP4 carries AAC.</summary>
    public string AudioCodec => IsWebm ? "libopus" : "aac";

    /// <summary>The picture format the encoder reads: Quick Sync wants NV12, the rest 4:2:0 planar.</summary>
    public string PixelFormat => Family == "intel" ? "nv12" : "yuv420p";

    /// <summary>Each encoder's own words for fast, balanced and quality, in the order of <see cref="Speeds"/>.</summary>
    private static readonly string[] X26xPresets = ["veryfast", "medium", "slow"];

    private static readonly string[] SvtAv1Presets = ["10", "8", "6"];

    private static readonly string[] NvencPresets = ["p2", "p4", "p6"];

    private static readonly string[] AmfQualities = ["speed", "balanced", "quality"];

    /// <summary>Bamo used cpu-used 4 at the good deadline; that is balanced here.</summary>
    private static readonly string[] VpxCpuUsed = ["5", "4", "2"];

    private string[] SpeedOption()
    {
        int step = Speeds.ToList().IndexOf(Speed);
        return (Family, Codec) switch
        {
            (Cpu, "av1") => ["-preset", SvtAv1Presets[step]],
            (Cpu, "vp9") => ["-deadline", "good", "-cpu-used", VpxCpuUsed[step], "-row-mt", "1", "-tune-content", "screen"],
            (Cpu, _) or ("intel", _) => ["-preset", X26xPresets[step]],
            ("nvidia", _) => ["-preset", NvencPresets[step]],
            _ => ["-quality", AmfQualities[step]]
        };
    }
}
