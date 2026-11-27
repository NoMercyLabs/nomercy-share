using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class HotkeyBinderTests
{
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeGlobalHotkeys _hotkeys = new();
    private readonly BarViewModel _bar;

    public HotkeyBinderTests()
    {
        _bar = new BarViewModel(
            new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
            new FakeScreenRecorder());
    }

    private HotkeyBinder Binder(HotkeySettings? settings = null) => new(_hotkeys, _bar, settings ?? new HotkeySettings());

    [Fact]
    public void StartRegistersTheChordsOfTheSettings()
    {
        Binder().Start();

        Assert.Equal(HotkeyChord.Parse("Ctrl+PrintScreen"), _hotkeys.Registered[BarAction.Region]);
        Assert.Equal(HotkeyChord.Parse("PrintScreen"), _hotkeys.Registered[BarAction.FullScreen]);
        Assert.Equal(HotkeyChord.Parse("Alt+PrintScreen"), _hotkeys.Registered[BarAction.Window]);
        Assert.Equal(HotkeyChord.Parse("Shift+PrintScreen"), _hotkeys.Registered[BarAction.Record]);
        Assert.Null(_bar.Notice);
    }

    [Fact]
    public void AnEmptySettingIsNeverRegistered()
    {
        Binder(new HotkeySettings { Record = "" }).Start();

        Assert.False(_hotkeys.Registered.ContainsKey(BarAction.Record));
        Assert.Equal(3, _hotkeys.Registered.Count);
    }

    [Fact]
    public async Task APressRunsTheActionTheWayAButtonClickDoes()
    {
        TaskCompletionSource captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _screen.OnCapture = () => captured.TrySetResult();
        Binder().Start();

        _hotkeys.Press(BarAction.Region);
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["region"], _screen.Calls);
    }

    [Fact]
    public void ATakenChordIsNamedInOneNoticeAndTheAppKeepsRunning()
    {
        _hotkeys.Taken.Add(HotkeyChord.Parse("Ctrl+PrintScreen"));
        Binder().Start();

        Assert.Equal("Hotkey Ctrl+PrintScreen is in use by another app", _bar.Notice);
        Assert.Equal(4, _hotkeys.Registered.Count);
    }

    [Fact]
    public void SeveralTakenChordsAreNamedInTheSameNotice()
    {
        _hotkeys.Taken.Add(HotkeyChord.Parse("Ctrl+PrintScreen"));
        _hotkeys.Taken.Add(HotkeyChord.Parse("PrintScreen"));
        Binder().Start();

        Assert.Equal("Hotkeys Ctrl+PrintScreen, PrintScreen are in use by another app", _bar.Notice);
    }

    [Fact]
    public void ATextThatIsNotAChordIsSkippedAndNamedInTheNotice()
    {
        Binder(new HotkeySettings { Window = "Ctrl+Banana" }).Start();

        Assert.False(_hotkeys.Registered.ContainsKey(BarAction.Window));
        Assert.Contains("Ctrl+Banana", _bar.Notice);
        Assert.Equal(3, _hotkeys.Registered.Count);
    }

    [Fact]
    public void DisposeReleasesTheHotkeysAndStopsListening()
    {
        HotkeyBinder binder = Binder();
        binder.Start();

        binder.Dispose();

        Assert.True(_hotkeys.Disposed);
        Assert.Equal(0, _hotkeys.PressedSubscribers);
    }
}
