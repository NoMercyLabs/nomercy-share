using Avalonia;
using Avalonia.Media;
using NoMercy.Share.App.Icons;

namespace NoMercy.Share.App.Tests;

public sealed class MoooomIconBoxTests
{
    private const double Box = 24;

    static MoooomIconBoxTests()
    {
        HeadlessApp.Ensure();
    }

    [Fact]
    public void EveryIconIsScaledByTheSameFactorFromTheSame24UnitBox()
    {
        Assert.NotEmpty(MoooomIcon.Names);
        foreach (string name in MoooomIcon.Names)
        {
            MoooomIcon icon = new() { Kind = name, Width = 48, Height = 48 };
            icon.Measure(new Size(48, 48));
            icon.Arrange(new Rect(0, 0, 48, 48));

            Assert.True(icon.Stretch == Stretch.None, $"{name} is stretched to its own bounds ({icon.Stretch})");
            ScaleTransform scale = Assert.IsType<ScaleTransform>(icon.RenderTransform);
            Assert.Equal(2, scale.ScaleX);
            Assert.Equal(2, scale.ScaleY);
        }
    }

    [Fact]
    public void EveryIconShapeLiesInsideTheBox()
    {
        foreach (string name in MoooomIcon.Names)
        {
            Rect bounds = new MoooomIcon { Kind = name }.Data!.Bounds;

            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= Box && bounds.Bottom <= Box, $"{name} leaves the 24-unit box: {bounds}");
        }
    }
}
