using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

public sealed class RecordingBorderRenderTests
{
    private const string RenderFolderVariable = "NOMERCY_SHARE_RENDER_DIR";

    private static readonly ScreenRect Primary = new(0, 0, 1920, 1080);
    private static readonly ScreenRect LeftAndAbove = new(-2560, -1440, 2560, 1440);

    [Fact]
    public void TheBorderShowsWhileTheRecorderReportsAnAreaAndGoesWhenItReportsNone() => HeadlessApp.Run(() =>
    {
        FakeScreenRecorder recorder = new() { Area = new ScreenRect(100, 100, 400, 300) };
        Exclusion exclusion = new(works: true);
        RecordingBorderPresenter presenter = new(recorder, () => [(Primary, 1.0)], exclusion);

        Start(recorder);
        RecordingBorderWindow window = Assert.IsType<RecordingBorderWindow>(presenter.Window);
        Assert.True(window.IsVisible);
        Assert.Single(exclusion.Windows);
        Assert.False(window.ShowActivated);
        Assert.False(window.ShowInTaskbar);
        Assert.True(window.Topmost);
        Assert.False(window.Focusable);

        recorder.Fail("ffmpeg ended");
        Dispatcher.UIThread.RunJobs();
        Assert.Null(presenter.Window);
        Assert.False(window.IsVisible);

        Start(recorder);
        RecordingBorderWindow second = presenter.Window!;
        Assert.True(recorder.StopAsync().IsCompletedSuccessfully);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(presenter.Window);
        Assert.False(second.IsVisible);
    });

    [Fact]
    public void TheWindowFollowsTheScaleOfAMonitorLeftOfAndAboveTheMainOne() => HeadlessApp.Run(() =>
    {
        FakeScreenRecorder recorder = new() { Area = new ScreenRect(-2000, -1000, 600, 300) };
        RecordingBorderPresenter presenter = new(recorder, () => [(Primary, 1.0), (LeftAndAbove, 1.5)], new Exclusion(works: true));

        Start(recorder);
        RecordingBorderWindow window = presenter.Window!;

        // 3 and 2 units at 150 % are 5 and 3 pixels.
        Assert.Equal(new PixelPoint(-2008, -1008), window.Position);
        Assert.Equal(616 / 1.5, window.Width, precision: 6);
        Assert.Equal(316 / 1.5, window.Height, precision: 6);
        Border top = window.FindControl<Border>("TopSide")!;
        Assert.Equal(5 / 1.5, top.Height, precision: 6);
        Assert.Equal(616 / 1.5, top.Width, precision: 6);
        Assert.True(recorder.StopAsync().IsCompletedSuccessfully);
        Dispatcher.UIThread.RunJobs();
    });

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ASideInsideTheRectangleShowsOnlyWhenTheWindowIsKeptOutOfCaptures(bool excluded, bool shown) => HeadlessApp.Run(() =>
    {
        RecordingBorderWindow window = new(new ScreenRect(0, 100, 400, 300), Primary, 1.0, new Exclusion(excluded));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(shown, window.FindControl<Border>("LeftSide")!.IsVisible);
        Assert.True(window.FindControl<Border>("RightSide")!.IsVisible);
        Assert.True(window.FindControl<Border>("TopSide")!.IsVisible);
        Assert.True(window.FindControl<Border>("BottomSide")!.IsVisible);
        window.Close();
    });

    [Fact]
    public void WithoutTheExclusionAFullScreenRecordingShowsNoSide() => HeadlessApp.Run(() =>
    {
        RecordingBorderWindow window = new(Primary, Primary, 1.0, new NoCaptureExclusion());
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.All(new[] { "TopSide", "BottomSide", "LeftSide", "RightSide" }, name => Assert.False(window.FindControl<Border>(name)!.IsVisible));
        window.Close();
    });

    [Theory]
    [InlineData("Dark", "border-dark.png")]
    [InlineData("Light", "border-light.png")]
    public void TheBorderIsDrawnInTheAccentColorAroundAnEmptyMiddle(string theme, string picture) => HeadlessApp.Run(() =>
    {
        RecordingBorderWindow window = new(new ScreenRect(100, 100, 320, 200), Primary, 1.0, new Exclusion(works: true))
        {
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(new PixelSize(330, 210), frame.PixelSize);
        Assert.Equal(0, Alpha(frame, 165, 105));
        Assert.Equal(255, Alpha(frame, 165, 1));
        Assert.Equal(255, Alpha(frame, 1, 105));
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            frame.Save(Path.Combine(folder, picture));
        }

        window.Close();
    });

    private static void Start(FakeScreenRecorder recorder)
    {
        Assert.True(recorder.StartAsync(new RecordingOptions()).IsCompletedSuccessfully);
        Dispatcher.UIThread.RunJobs();
    }

    private static byte Alpha(WriteableBitmap frame, int x, int y)
    {
        using Avalonia.Platform.ILockedFramebuffer pixels = frame.Lock();
        return System.Runtime.InteropServices.Marshal.ReadByte(pixels.Address, (y * pixels.RowBytes) + (x * 4) + 3);
    }

    private sealed class Exclusion(bool works) : ICaptureExclusion
    {
        public List<nint> Windows { get; } = [];

        public bool Exclude(nint window)
        {
            Windows.Add(window);
            return works;
        }
    }
}
