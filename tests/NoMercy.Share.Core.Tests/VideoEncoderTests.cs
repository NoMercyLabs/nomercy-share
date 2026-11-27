using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

/// <summary>The codec, encoder and speed a user picks become the ffmpeg encoder and its options; the defaults give today's output.</summary>
public sealed class VideoEncoderTests
{
    private static readonly VideoPlan Plan = new(1280, 720, 2000);

    [Fact]
    public void TheDefaultsAreTheCpuH264FastEncodeOfBefore()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", Plan, encoder: VideoEncoder.From(new RecordingSettings()));

        Assert.Equal(["-c:v", "libx264", "-preset", "veryfast", "-b:v", "2000k", "-maxrate", "2000k", "-bufsize", "4000k", "-pix_fmt", "yuv420p"], Slice(args, "-c:v", 12));
    }

    [Theory]
    [InlineData("h264", "nvidia", "balanced", "h264_nvenc", "-preset", "p4")]
    [InlineData("hevc", "nvidia", "quality", "hevc_nvenc", "-preset", "p6")]
    [InlineData("hevc", "cpu", "balanced", "libx265", "-preset", "medium")]
    [InlineData("av1", "cpu", "fast", "libsvtav1", "-preset", "10")]
    [InlineData("h264", "intel", "quality", "h264_qsv", "-preset", "slow")]
    [InlineData("av1", "amd", "fast", "av1_amf", "-quality", "speed")]
    public void EachEncoderGetsItsOwnNameAndSpeedOption(string codec, string family, string speed, string name, string option, string value)
    {
        VideoEncoder encoder = VideoEncoder.From(new RecordingSettings { Codec = codec, Encoder = family, Speed = speed });

        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", Plan, encoder: encoder);

        Assert.Equal(["-c:v", name, option, value], Slice(args, "-c:v", 4));
    }

    [Fact]
    public void HevcIsTaggedSoAppleAndBrowsersPlayIt()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", Plan, encoder: VideoEncoder.From(new RecordingSettings { Codec = "hevc", Encoder = "nvidia" }));

        Assert.Equal(["-tag:v", "hvc1"], Slice(args, "-tag:v", 2));
    }

    [Fact]
    public void QuickSyncGetsTheNv12PictureItReads()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", Plan, encoder: VideoEncoder.From(new RecordingSettings { Encoder = "intel" }));

        Assert.Equal(["-pix_fmt", "nv12"], Slice(args, "-pix_fmt", 2));
    }

    [Fact]
    public void AnUnknownValueInTheFileFallsBackToTheDefault()
    {
        VideoEncoder encoder = VideoEncoder.From(new RecordingSettings { Codec = "vp7", Encoder = "voodoo", Speed = "warp" });

        Assert.Equal(VideoEncoder.From(new RecordingSettings()), encoder);
    }

    [Fact]
    public void TheCpuStandInIsTheSameCodecAndSpeedOnTheCpu()
    {
        VideoEncoder gpu = VideoEncoder.From(new RecordingSettings { Codec = "hevc", Encoder = "nvidia", Speed = "quality" });

        Assert.Equal(new VideoEncoder("hevc", "cpu", "quality"), gpu.OnCpu());
    }

    [Fact]
    public void TheProbeEncodesAFewBlackFramesToNothing()
    {
        IReadOnlyList<string> args = FfmpegArguments.Probe(new VideoEncoder("h264", "nvidia", "fast"));

        Assert.Contains("h264_nvenc", args);
        Assert.Equal(["-f", "null", "-"], args.Skip(args.Count - 3));
    }

    [Fact]
    public void Vp9IsBamosTwoPassWebmTunedForScreens()
    {
        VideoEncoder vp9 = VideoEncoder.From(new RecordingSettings { Codec = "vp9", Speed = "balanced" });

        Assert.True(vp9.IsTwoPass);
        Assert.Equal((".webm", "video/webm", "libopus"), (vp9.Extension, vp9.ContentType, vp9.AudioCodec));
        Assert.Equal(["-c:v", "libvpx-vp9", "-deadline", "good", "-cpu-used", "4", "-row-mt", "1", "-tune-content", "screen"], Slice(vp9.Arguments(), "-c:v", 10));
        Assert.False(vp9.TakesPeakRate);
    }

    [Fact]
    public void Mp4CodecsAreOnePassWithAac()
    {
        VideoEncoder h264 = VideoEncoder.From(new RecordingSettings());

        Assert.False(h264.IsTwoPass);
        Assert.Equal((".mp4", "video/mp4", "aac"), (h264.Extension, h264.ContentType, h264.AudioCodec));
    }

    [Fact]
    public void PassOneOnlyStudiesThePictureAndWritesNoFile()
    {
        VideoEncoder vp9 = new("vp9", "cpu", "balanced");

        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.webm", Plan, encoder: vp9, pass: new EncodePass(1, "C:/tmp/log"));

        Assert.Equal(["-pass", "1", "-passlogfile", "C:/tmp/log"], Slice(args, "-pass", 4));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("out.webm", args);
        Assert.Equal(["-f", "null", "NUL"], args.Skip(args.Count - 3));
    }

    [Fact]
    public void PassTwoWritesTheWebmWithOpusAtThePlannedRate()
    {
        VideoEncoder vp9 = new("vp9", "cpu", "balanced");
        AudioTrack[] sound = [new AudioTrack("C:/tmp/sound.wav", 0)];

        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.webm", Plan with { AudioKbps = 64 }, sound, encoder: vp9, pass: new EncodePass(2, "C:/tmp/log"));

        Assert.Equal(["-pass", "2", "-passlogfile", "C:/tmp/log"], Slice(args, "-pass", 4));
        Assert.Equal(["-c:a", "libopus", "-b:a", "64k"], Slice(args, "-c:a", 4));
        Assert.DoesNotContain("+faststart", args);
        Assert.Equal("out.webm", args[^1]);
    }

    [Fact]
    public void ALowerPlannedFrameRateIsSetOnTheOutput()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", Plan with { FrameRate = 15 });

        Assert.Equal(["-r", "15"], Slice(args, "-r", 2));
    }

    private static IReadOnlyList<string> Slice(IReadOnlyList<string> args, string first, int count)
    {
        int at = args.ToList().IndexOf(first);
        Assert.True(at >= 0, $"{first} missing from: {string.Join(' ', args)}");
        return [.. args.Skip(at).Take(count)];
    }
}
