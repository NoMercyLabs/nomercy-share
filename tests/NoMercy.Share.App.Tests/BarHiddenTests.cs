using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

public sealed class BarHiddenTests
{
    [Fact]
    public void TheCrossHidesTheBarAndTheAppStaysOn() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        window.Summon();
        bar.ActionCommand.Execute(BarAction.Close);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
        Assert.False(window.IsClosed);
    }));

    [Fact]
    public void ANoticeWhileHiddenShowsTheBarUntilTheNoticeEnds() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        bar.ShowMessage("Link copied");
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.IsVisible);

        bar.ClearNotice();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsVisible);
    }));

    [Fact]
    public void ANoticeOnACalledBarLeavesItOpen() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        window.Summon();
        bar.ShowMessage("Link copied");
        bar.ClearNotice();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsVisible);
    }));

    [Fact]
    public void AQuestionWhileHiddenStaysUntilItIsAnswered() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        bar.CheckFolderAccess();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(bar.Ask);
        Assert.True(window.IsVisible);

        bar.AskNoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsVisible);
    }));

    [Fact]
    public void ACaptureLeavesAHiddenBarHidden() => HeadlessApp.Run(() => WithBar((window, _) =>
    {
        BarWindowHost host = new(() => window);

        Pump(host.HideAsync());
        Pump(host.ShowAsync());

        Assert.False(window.IsVisible);
    }));

    [Fact]
    public void TheBarHotkeyOpensAHiddenBarThatStays() => HeadlessApp.Run(() => WithBar((window, bar) =>
    {
        Pump(bar.RunAsync(BarAction.Show));

        Assert.True(window.IsVisible);
        bar.ShowMessage("Link copied");
        bar.ClearNotice();
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.IsVisible);
    }));

    /// <summary>The test runs on the UI thread, so it must run the queued work itself; blocking on the task would hang.</summary>
    private static void Pump(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully, "the UI work did not finish");
    }

    [Fact]
    public void ACaptureShowsACalledBarAgain() => HeadlessApp.Run(() => WithBar((window, _) =>
    {
        BarWindowHost host = new(() => window);
        window.Summon();

        Pump(host.HideAsync());
        Assert.False(window.IsVisible);
        Pump(host.ShowAsync());

        Assert.True(window.IsVisible);
    }));

    private static void WithBar(Action<BarWindow, BarViewModel> test)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-hidden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // A captures folder under a file can never be written, so the folder question can be raised; nothing runs a capture or a share.
            File.WriteAllText(Path.Combine(root, "file"), "x");
            CaptureFiles files = new(new CaptureSettings { Folder = Path.Combine(root, "file", "Captures") }, "unused", () => DateTimeOffset.UnixEpoch, Path.Combine(root, "fallback"));
            // The bar hotkey starts an area screenshot; this one is canceled at once, as with Escape.
            CaptureFlow capture = new(new FakeScreenCapture { Throws = new OperationCanceledException() }, new FakeCaptureHost(),
                new CaptureSettings { HideDelayMilliseconds = 0 });
            BarViewModel bar = new(capture, null!, files: files, folderAccess: new AlwaysOn());
            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings());
            test(window, bar);
            window.Quit();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class AlwaysOn : IFolderAccess
    {
        public bool IsProtectionOn() => true;

        public Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
