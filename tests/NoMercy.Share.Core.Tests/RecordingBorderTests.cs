using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class RecordingBorderTests
{
    private const int Thickness = 3;
    private const int Gap = 2;

    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void WithRoomEverySideLiesOutsideTheRecordedRectangle()
    {
        ScreenRect recorded = new(100, 200, 400, 300);

        RecordingBorder border = RecordingBorder.Around(recorded, Monitor, Thickness, Gap);

        Assert.Equal(new ScreenRect(95, 195, 410, 310), border.Bounds);
        Assert.Equal(new ScreenRect(95, 195, 410, 3), border.Top);
        Assert.Equal(new ScreenRect(95, 502, 410, 3), border.Bottom);
        Assert.Equal(new ScreenRect(95, 198, 3, 304), border.Left);
        Assert.Equal(new ScreenRect(502, 198, 3, 304), border.Right);
        Assert.All(Sides(border), side => Assert.True(side.Intersect(recorded).IsEmpty));
    }

    [Fact]
    public void AFullScreenRectangleGetsEverySideJustInsideTheMonitor()
    {
        RecordingBorder border = RecordingBorder.Around(Monitor, Monitor, Thickness, Gap);

        Assert.Equal(Monitor, border.Bounds);
        Assert.Equal(new ScreenRect(0, 0, 1920, 3), border.Top);
        Assert.Equal(new ScreenRect(0, 1077, 1920, 3), border.Bottom);
        Assert.Equal(new ScreenRect(0, 3, 3, 1074), border.Left);
        Assert.Equal(new ScreenRect(1917, 3, 3, 1074), border.Right);
    }

    [Fact]
    public void OnlyTheSideWithoutRoomGoesInside()
    {
        ScreenRect recorded = new(4, 200, 400, 300); // 4 pixels is less than the gap and the thickness

        RecordingBorder border = RecordingBorder.Around(recorded, Monitor, Thickness, Gap);

        Assert.Equal(new ScreenRect(4, 198, 3, 304), border.Left);
        Assert.Equal(new ScreenRect(4, 195, 405, 3), border.Top);
        Assert.True(border.Top.Intersect(recorded).IsEmpty);
        Assert.True(border.Right.Intersect(recorded).IsEmpty);
        Assert.True(border.Bottom.Intersect(recorded).IsEmpty);
    }

    [Fact]
    public void ExactlyEnoughRoomKeepsTheSideOutside()
    {
        RecordingBorder border = RecordingBorder.Around(new ScreenRect(5, 5, 1910, 1070), Monitor, Thickness, Gap);

        Assert.Equal(Monitor, border.Bounds);
        Assert.All(Sides(border), side => Assert.True(side.Intersect(new ScreenRect(5, 5, 1910, 1070)).IsEmpty));
    }

    [Fact]
    public void AMonitorLeftOfAndAboveTheMainOneKeepsItsNegativeCoordinates()
    {
        ScreenRect monitor = new(-2560, -1440, 2560, 1440);

        RecordingBorder border = RecordingBorder.Around(new ScreenRect(-2560, -1000, 800, 600), monitor, Thickness, Gap);

        Assert.Equal(new ScreenRect(-2560, -1005, 805, 610), border.Bounds);
        Assert.Equal(new ScreenRect(-2560, -1002, 3, 604), border.Left);
        Assert.Equal(new ScreenRect(-1758, -1002, 3, 604), border.Right);
    }

    private static ScreenRect[] Sides(RecordingBorder border) => [border.Top, border.Bottom, border.Left, border.Right];
}
