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

    private static IReadOnlyList<string> Slice(IReadOnlyList<string> args, string first, int count)
    {
        int at = args.ToList().IndexOf(first);
        Assert.True(at >= 0, $"{first} missing from: {string.Join(' ', args)}");
        return [.. args.Skip(at).Take(count)];
    }
}
