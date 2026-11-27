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
    public void ANumberIsWrittenWithoutAThousandsSeparatorInAnyCulture()
    {
        IReadOnlyList<string> args = FfmpegArguments.Compress("in.mkv", "out.mp4", new VideoPlan(1920, 1080, 12345));

        Assert.Contains("12345k", args);
        Assert.Contains("24690k", args);
    }
}
