using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class FfmpegArgumentsTests
{
    [Fact]
    public void TheRecordPassGrabsOneMonitorRegionWithTheDesktopDuplicationFilterAndWritesLosslessishH264()
    {
        RecordingTarget target = new(MonitorIndex: 1, OffsetX: 100, OffsetY: 50, Width: 320, Height: 240);

        IReadOnlyList<string> args = FfmpegArguments.Record(target, frameRate: 30, outputPath: "C:/caps/a.mkv");

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats",
            "-f", "lavfi", "-i", "ddagrab=output_idx=1:framerate=30:draw_mouse=1:offset_x=100:offset_y=50:video_size=320x240",
            "-vf", "hwdownload,format=bgra",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
            "-y", "C:/caps/a.mkv"
        ], args);
    }

    [Fact]
    public void TheFallbackRecordPassGrabsTheSameAreaOfTheDesktopWithGdi()
    {
        IReadOnlyList<string> args = FfmpegArguments.RecordWithGdi(new ScreenRect(-1820, 50, 320, 240), frameRate: 30, outputPath: "C:/caps/a.mkv", flushEachPacket: true);

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats",
            "-f", "gdigrab", "-framerate", "30", "-draw_mouse", "1", "-offset_x", "-1820", "-offset_y", "50", "-video_size", "320x240", "-i", "desktop",
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
            "-flush_packets", "1",
            "-y", "C:/caps/a.mkv"
        ], args);
    }

    [Fact]
    public void TheDesktopDuplicationProbeGrabsOneFrameOfTheSameInputAndWritesNoFile()
    {
        RecordingTarget target = new(MonitorIndex: 1, OffsetX: 100, OffsetY: 50, Width: 320, Height: 240);

        IReadOnlyList<string> args = FfmpegArguments.ProbeDesktopDuplication(target, frameRate: 30);

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats",
            "-f", "lavfi", "-i", "ddagrab=output_idx=1:framerate=30:draw_mouse=1:offset_x=100:offset_y=50:video_size=320x240",
            "-vf", "hwdownload,format=bgra",
            "-frames:v", "1", "-f", "null", "-"
        ], args);
    }

    [Fact]
    public void ARecordPassThatTimesTheSoundWritesEachPacketAtOnceSoTheFileShowsTheFirstFrame()
    {
        IReadOnlyList<string> args = FfmpegArguments.Record(new RecordingTarget(0, 0, 0, 2, 2), 30, "a.mkv", flushEachPacket: true);

        Assert.Equal(["-flush_packets", "1", "-y", "a.mkv"], args.Skip(args.Count - 4));
        Assert.DoesNotContain("-flush_packets", FfmpegArguments.Record(new RecordingTarget(0, 0, 0, 2, 2), 30, "a.mkv"));
    }

    [Fact]
    public void ThePathStaysOneArgumentEvenWithSpacesAndQuotes()
    {
        IReadOnlyList<string> args = FfmpegArguments.Record(new RecordingTarget(0, 0, 0, 2, 2), 30, "C:/my caps/it's \"a\".mkv");

        Assert.Equal("C:/my caps/it's \"a\".mkv", args[^1]);
    }

    [Fact]
    public void TheCompressPassScalesToThePlanAndHoldsTheBitrateToTheBudgetInOnePass()
    {
        VideoPlan plan = new(Width: 876, Height: 494, VideoKbps: 648);

        IReadOnlyList<string> args = FfmpegArguments.Compress("C:/caps/a.mkv", "C:/caps/a.mp4", plan);

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats", "-nostdin",
            "-i", "C:/caps/a.mkv",
            "-vf", "scale=876:494:flags=lanczos",
            "-c:v", "libx264", "-preset", "veryfast",
            "-b:v", "648k", "-maxrate", "648k", "-bufsize", "1296k",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-an",
            "-y", "C:/caps/a.mp4"
        ], args);
    }

    [Fact]
    public void WithNoSoundTheCompressPassIsTheVideoOnlyOneByteForByte()
    {
        VideoPlan plan = new(Width: 876, Height: 494, VideoKbps: 648);

        IReadOnlyList<string> silent = FfmpegArguments.Compress("a.mkv", "a.mp4", plan);

        Assert.Equal(silent, FfmpegArguments.Compress("a.mkv", "a.mp4", plan, [], 128));
        Assert.Contains("-an", silent);
        Assert.DoesNotContain("-c:a", silent);
    }

    [Fact]
    public void OneSoundFileIsAnExtraInputShiftedByItsOffsetAndEncodedAsAac()
    {
        VideoPlan plan = new(Width: 876, Height: 494, VideoKbps: 648);

        IReadOnlyList<string> args = FfmpegArguments.Compress("a.mkv", "a.mp4", plan, [new AudioTrack("a-system.wav", 0.1234)], 128);

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats", "-nostdin",
            "-i", "a.mkv",
            "-itsoffset", "0.123", "-i", "a-system.wav",
            "-vf", "scale=876:494:flags=lanczos",
            "-c:v", "libx264", "-preset", "veryfast",
            "-b:v", "648k", "-maxrate", "648k", "-bufsize", "1296k",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-map", "0:v:0", "-map", "1:a:0", "-c:a", "aac", "-b:a", "128k",
            "-y", "a.mp4"
        ], args);
        Assert.DoesNotContain("-an", args);
        Assert.DoesNotContain("-filter_complex", args);
    }

    [Fact]
    public void TwoSoundFilesAreMixedAtFullLevelAndTheFirstOneSetsTheLength()
    {
        VideoPlan plan = new(Width: 876, Height: 494, VideoKbps: 648);

        IReadOnlyList<string> args = FfmpegArguments.Compress(
            "a.mkv", "a.mp4", plan, [new AudioTrack("a-system.wav", 0.05), new AudioTrack("a-mic.wav", 0.2)], 96);

        Assert.Equal(
        [
            "-hide_banner", "-loglevel", "warning", "-nostats", "-nostdin",
            "-i", "a.mkv",
            "-itsoffset", "0.050", "-i", "a-system.wav",
            "-itsoffset", "0.200", "-i", "a-mic.wav",
            "-vf", "scale=876:494:flags=lanczos",
            "-filter_complex",
            "[1:a]aresample=async=1:first_pts=0[s0];[2:a]aresample=async=1:first_pts=0[s1];[s0][s1]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[mix]",
            "-c:v", "libx264", "-preset", "veryfast",
            "-b:v", "648k", "-maxrate", "648k", "-bufsize", "1296k",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-map", "0:v:0", "-map", "[mix]", "-c:a", "aac", "-b:a", "96k",
            "-y", "a.mp4"
        ], args);
        Assert.DoesNotContain("-an", args);
    }

    [Fact]
    public void ANumberIsWrittenWithoutAThousandsSeparatorInAnyCulture()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", new VideoPlan(1920, 1080, 12345));

        Assert.Contains("12345k", args);
        Assert.Contains("24690k", args);
    }
}
