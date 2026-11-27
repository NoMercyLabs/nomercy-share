using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

public sealed class RegionMathTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);
    private static readonly PixelRect NoOrigin = new(0, 0, 0, 0);

    [Theory]
    [InlineData(100, 50, 300, 250)]
    [InlineData(300, 250, 100, 50)]
    [InlineData(300, 50, 100, 250)]
    [InlineData(100, 250, 300, 50)]
    public void ADragInAnyDirectionGivesTheSameRectangle(double ax, double ay, double bx, double by)
    {
        Assert.Equal(new PixelRect(100, 50, 200, 200), RegionMath.FromDrag(ax, ay, bx, by, 1, NoOrigin, Screen));
    }

    [Fact]
    public void ADragPastTheEdgesIsClampedToTheScreen()
    {
        PixelRect rect = RegionMath.FromDrag(-50, -20, 5000, 4000, 1, NoOrigin, Screen);

        Assert.Equal(Screen, rect);
    }

    [Fact]
    public void ADragOnASecondScreenIsClampedToTheVirtualScreenWithItsOrigin()
    {
        PixelRect virtualScreen = new(-1920, 0, 3840, 1080);
        PixelRect leftOrigin = new(-1920, 0, 0, 0);

        PixelRect rect = RegionMath.FromDrag(10, 10, 110, 60, 1, leftOrigin, virtualScreen);

        Assert.Equal(new PixelRect(-1910, 10, 100, 50), rect);
    }

    [Fact]
    public void DeviceIndependentPointsAreScaledToPhysicalPixels()
    {
        PixelRect rect = RegionMath.FromDrag(100, 50, 300, 250, 1.5, NoOrigin, Screen);

        Assert.Equal(new PixelRect(150, 75, 300, 300), rect);
    }

    [Fact]
    public void TheMonitorOriginIsAddedAfterScaling()
    {
        PixelRect rect = RegionMath.FromDrag(100, 50, 300, 250, 2, new PixelRect(1920, 0, 0, 0), new PixelRect(0, 0, 5760, 2160));

        Assert.Equal(new PixelRect(2120, 100, 400, 400), rect);
    }

    [Fact]
    public void ADragOfNoSizeIsEmpty()
    {
        Assert.True(RegionMath.FromDrag(10, 10, 10, 90, 1, NoOrigin, Screen).IsEmpty);
    }

    [Fact]
    public void TheSizeLabelSitsBelowTheSelectionWhenThereIsRoom()
    {
        (double x, double y) = RegionMath.LabelPosition(new PixelRect(100, 100, 200, 100), 80, 20, 8, 1920, 1080);

        Assert.Equal((100d, 208d), (x, y));
    }

    [Fact]
    public void TheSizeLabelMovesAboveWhenTheSelectionTouchesTheBottom()
    {
        (double x, double y) = RegionMath.LabelPosition(new PixelRect(100, 900, 200, 180), 80, 20, 8, 1920, 1080);

        Assert.Equal((100d, 872d), (x, y));
    }

    [Fact]
    public void TheSizeLabelMovesInsideWhenThereIsNoRoomAboveOrBelow()
    {
        (double x, double y) = RegionMath.LabelPosition(new PixelRect(0, 0, 1920, 1080), 80, 20, 8, 1920, 1080);

        Assert.Equal((8d, 8d), (x, y));
    }

    [Fact]
    public void TheSizeLabelStaysInsideTheRightEdge()
    {
        (double x, _) = RegionMath.LabelPosition(new PixelRect(1900, 100, 20, 20), 80, 20, 8, 1920, 1080);

        Assert.Equal(1840d, x);
    }
}
