using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class RegionMathTests
{
    private static readonly ScreenRect Screen = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(100, 50, 300, 250)]
    [InlineData(300, 250, 100, 50)]
    [InlineData(300, 50, 100, 250)]
    [InlineData(100, 250, 300, 50)]
    public void ADragInAnyDirectionGivesTheSameRectangle(double ax, double ay, double bx, double by)
    {
        Assert.Equal(new ScreenRect(100, 50, 200, 200), RegionMath.FromDrag(ax, ay, bx, by, 1, 0, 0, Screen));
    }

    [Fact]
    public void ADragPastTheEdgesIsClampedToTheScreen()
    {
        ScreenRect rect = RegionMath.FromDrag(-50, -20, 5000, 4000, 1, 0, 0, Screen);

        Assert.Equal(Screen, rect);
    }

    [Fact]
    public void ADragOnASecondScreenIsClampedToTheVirtualScreenWithItsOrigin()
    {
        ScreenRect virtualScreen = new(-1920, 0, 3840, 1080);

        ScreenRect rect = RegionMath.FromDrag(10, 10, 110, 60, 1, -1920, 0, virtualScreen);

        Assert.Equal(new ScreenRect(-1910, 10, 100, 50), rect);
    }

    [Fact]
    public void DeviceIndependentPointsAreScaledToPhysicalPixels()
    {
        ScreenRect rect = RegionMath.FromDrag(100, 50, 300, 250, 1.5, 0, 0, Screen);

        Assert.Equal(new ScreenRect(150, 75, 300, 300), rect);
    }

    [Fact]
    public void TheMonitorOriginIsAddedAfterScaling()
    {
        ScreenRect rect = RegionMath.FromDrag(100, 50, 300, 250, 2, 1920, 0, new ScreenRect(0, 0, 5760, 2160));

        Assert.Equal(new ScreenRect(2120, 100, 400, 400), rect);
    }

    [Fact]
    public void TwoRectanglesIntersectInTheirSharedPart()
    {
        Assert.Equal(new ScreenRect(50, 40, 50, 60), new ScreenRect(0, 0, 100, 100).Intersect(new ScreenRect(50, 40, 200, 200)));
        Assert.True(new ScreenRect(0, 0, 10, 10).Intersect(new ScreenRect(20, 20, 10, 10)).IsEmpty);
    }

    [Fact]
    public void ADragOfNoSizeIsEmpty()
    {
        Assert.True(RegionMath.FromDrag(10, 10, 10, 90, 1, 0, 0, Screen).IsEmpty);
    }

    [Fact]
    public void TheSizeLabelSitsBelowTheSelectionWhenThereIsRoom()
    {
        (double x, double y) = RegionMath.LabelPosition(new ScreenRect(100, 100, 200, 100), 80, 20, 8, 1920, 1080);

        Assert.Equal((100d, 208d), (x, y));
    }

    [Fact]
    public void TheSizeLabelMovesAboveWhenTheSelectionTouchesTheBottom()
    {
        (double x, double y) = RegionMath.LabelPosition(new ScreenRect(100, 900, 200, 180), 80, 20, 8, 1920, 1080);

        Assert.Equal((100d, 872d), (x, y));
    }

    [Fact]
    public void TheSizeLabelMovesInsideWhenThereIsNoRoomAboveOrBelow()
    {
        (double x, double y) = RegionMath.LabelPosition(new ScreenRect(0, 0, 1920, 1080), 80, 20, 8, 1920, 1080);

        Assert.Equal((8d, 8d), (x, y));
    }

    [Fact]
    public void TheSizeLabelStaysInsideTheRightEdge()
    {
        (double x, _) = RegionMath.LabelPosition(new ScreenRect(1900, 100, 20, 20), 80, 20, 8, 1920, 1080);

        Assert.Equal(1840d, x);
    }
}
