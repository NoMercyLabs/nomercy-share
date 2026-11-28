using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Capture;

/// <summary>Which still capture a bar button asks for.</summary>
public enum CaptureKind
{
    Region,
    Window,
    FullScreen
}

/// <summary>The window that must not appear in a capture. The app registers the bar behind this.</summary>
public interface ICaptureHost
{
    Task HideAsync(CancellationToken cancellationToken = default);

    Task ShowAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lets the user drag a rectangle on a frozen picture of the screen. The UI toolkit implements it.</summary>
public interface IRegionSelector
{
    /// <summary>Shows the frame and returns the chosen rectangle in virtual screen pixels, or null when the user cancels.</summary>
    Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default);
}

/// <summary>A still capture: hide the bar, take the picture (which saves the PNG), bring the bar back. Sharing it is the share flow's job.</summary>
public sealed class CaptureFlow(
    IScreenCapture screen,
    ICaptureHost host,
    CaptureSettings settings,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    /// <summary>Runs one capture. A cancelled capture throws <see cref="OperationCanceledException"/> and touches nothing else.</summary>
    public Task<CaptureResult> RunAsync(CaptureKind kind, CancellationToken cancellationToken = default) =>
        WhileHiddenAsync(() => CaptureAsync(kind, cancellationToken), cancellationToken);

    /// <summary>Runs work with the bar out of the way, so the previous window is the active one again.</summary>
    public async Task<T> WhileHiddenAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        await host.HideAsync(cancellationToken);
        try
        {
            await _delay(TimeSpan.FromMilliseconds(settings.HideDelayMilliseconds), cancellationToken);
            return await work();
        }
        finally
        {
            await host.ShowAsync(CancellationToken.None);
        }
    }

    private Task<CaptureResult> CaptureAsync(CaptureKind kind, CancellationToken cancellationToken) => kind switch
    {
        CaptureKind.Region => screen.CaptureRegionAsync(cancellationToken),
        CaptureKind.Window => screen.CaptureWindowAsync(cancellationToken),
        _ => screen.CaptureFullScreenAsync(cancellationToken)
    };
}
