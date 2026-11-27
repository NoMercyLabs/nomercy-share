using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class RecordingTargetTests
{
    private static readonly ScreenRect[] TwoMonitors = [new(0, 0, 1920, 1080), new(1920, -200, 2560, 1440)];

    [Fact]
    public void ARegionOnTheFirstMonitorUsesItsIndexAndOffsetsFromItsCorner()
    {
        RecordingTarget target = RecordingTarget.For(new ScreenRect(100, 50, 320, 240), TwoMonitors);

        Assert.Equal(new RecordingTarget(0, 100, 50, 320, 240), target);
    }

    [Fact]
    public void ARegionOnASecondMonitorWithANegativeTopIsMeasuredFromThatMonitor()
    {
        RecordingTarget target = RecordingTarget.For(new ScreenRect(2000, -100, 640, 360), TwoMonitors);

        Assert.Equal(new RecordingTarget(1, 80, 100, 640, 360), target);
    }

    [Fact]
    public void ARegionOverTwoMonitorsGoesToTheOneItCoversMostAndIsCutAtItsEdge()
    {
        RecordingTarget target = RecordingTarget.For(new ScreenRect(1800, 0, 400, 200), TwoMonitors);

        Assert.Equal(new RecordingTarget(1, 0, 200, 280, 200), target);
    }

    [Fact]
    public void ARegionOnNoMonitorIsRefusedWithAPlainMessage()
    {
        RecordingException error = Assert.Throws<RecordingException>(() => RecordingTarget.For(new ScreenRect(9000, 9000, 10, 10), TwoMonitors));

        Assert.Equal("The chosen region is not on a screen.", error.Message);
    }
}
