using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App.Tests;

/// <summary>A bar that was on screen comes back above the open area picker, so another choice can be clicked.</summary>
public sealed class BarAbovePickerTests
{
    [Fact]
    public void ACalledBarHiddenForThePictureReturnsAboveTheOpenPicker() => HeadlessApp.Run(() => WhilePicking(called: true, (window, raises) =>
    {
        Assert.True(window.IsVisible);
        Assert.True(window.Topmost);
        Assert.Contains(true, raises());
    }));

    [Fact]
    public void AHiddenBarStaysHiddenWhileAPickerIsOpen() => HeadlessApp.Run(() => WhilePicking(called: false, (window, raises) =>
    {
        Assert.False(window.IsVisible);
        Assert.Empty(raises());
    }));

    /// <summary>Opens the real picker the way a screenshot does, with the bar already hidden for the picture.</summary>
    private static void WhilePicking(bool called, Action<BarWindow, Func<IReadOnlyList<bool>>> check)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-picker-{Guid.NewGuid():N}");
        BarWindow window = new(new BarViewModel(null!, null!), new BarPositionStore(new SettingsFile(Path.Combine(root, "settings.json"))), new BarSettings());
        try
        {
            if (called)
            {
                window.Summon();
            }

            window.HideForCapture();
            // A change of Topmost is how the window is put back on top of the topmost windows.
            List<bool> raises = [];
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowBase.TopmostProperty)
                {
                    raises.Add(window.Topmost);
                }
            };
            ScreenFrame frozen = new(0, 0, 64, 64, new byte[64 * 64 * ScreenFrame.BytesPerPixel]);
            using CancellationTokenSource cancel = new();
            Task<ScreenRect?> picking = new AvaloniaRegionSelector(() => window, new NoForegroundGrant()).SelectAsync(frozen, cancel.Token);
            Dispatcher.UIThread.RunJobs();
            Assert.False(picking.IsCompleted, "the picker did not open");

            check(window, () => raises);

            cancel.Cancel();
            Stopwatch waited = Stopwatch.StartNew();
            while (!picking.IsCompleted && waited.Elapsed < TimeSpan.FromSeconds(10))
            {
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(picking.IsCompletedSuccessfully, "the canceled picker did not close");
        }
        finally
        {
            window.Quit();
        }
    }
}
