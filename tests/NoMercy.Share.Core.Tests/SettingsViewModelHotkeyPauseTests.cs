using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>While a hotkey field has focus the global hotkeys are off, so the field can record a chord the app itself serves.</summary>
public sealed class SettingsViewModelHotkeyPauseTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-pause-test-{Guid.NewGuid():N}");
    private readonly FakeGlobalHotkeys _hotkeys = new();
    private readonly SettingsViewModel _model;

    public SettingsViewModelHotkeyPauseTests()
    {
        SettingsFile file = new(SettingsFile.DefaultPath(_folder));
        LiveSettings live = new(file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        HotkeyBinder binder = new(_hotkeys, bar, live.Current.Hotkeys);
        binder.Start();
        _model = new SettingsViewModel(file, live, new FakeCredentialStore(), binder, ["r2", "s3"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void FocusOnAHotkeyFieldPausesTheHotkeysAndLosingFocusResumesThem()
    {
        _model.HotkeyFieldFocused();
        Assert.Equal(1, _hotkeys.PausesHeld);

        _model.HotkeyFieldLeft();
        Assert.Equal(0, _hotkeys.PausesHeld);
    }

    [Fact]
    public void FocusMovingFromOneFieldToAnotherNeverHoldsTwoPauses()
    {
        _model.HotkeyFieldFocused();
        _model.HotkeyFieldFocused();
        Assert.Equal(1, _hotkeys.PausesHeld);

        _model.HotkeyFieldLeft();
        Assert.Equal(0, _hotkeys.PausesHeld);
    }

    [Fact]
    public void ClosingTheWindowResumesTheHotkeys()
    {
        _model.HotkeyFieldFocused();

        _model.ReleaseHotkeys();

        Assert.Equal(0, _hotkeys.PausesHeld);
    }

    [Fact]
    public void CancelResumesTheHotkeys()
    {
        _model.HotkeyFieldFocused();

        _model.Cancel();

        Assert.Equal(0, _hotkeys.PausesHeld);
    }

    [Fact]
    public void ASaveResumesTheHotkeysBeforeItRegistersTheNewChords()
    {
        _model.HotkeyFieldFocused();
        int pausesWhenRegistered = -1;
        _hotkeys.OnRegister = () => pausesWhenRegistered = _hotkeys.PausesHeld;

        _model.Save();

        Assert.True(_model.Saved);
        Assert.Equal(0, pausesWhenRegistered);
        Assert.Equal(0, _hotkeys.PausesHeld);
    }

    [Fact]
    public void LosingFocusWithoutEverGainingItHoldsNothing()
    {
        _model.HotkeyFieldLeft();

        Assert.Equal(0, _hotkeys.PauseCalls);
        Assert.Equal(0, _hotkeys.PausesHeld);
    }
}
