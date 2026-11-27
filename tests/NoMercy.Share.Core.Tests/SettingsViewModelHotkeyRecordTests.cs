using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

/// <summary>While a hotkey field has focus the app records the chords the shell would take, and hands them to that field.</summary>
public sealed class SettingsViewModelHotkeyRecordTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-record-test-{Guid.NewGuid():N}");
    private readonly FakeGlobalHotkeys _hotkeys = new();
    private readonly SettingsViewModel _model;

    public SettingsViewModelHotkeyRecordTests()
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

    private HotkeyRow Row(BarAction action) => _model.Hotkeys.First(row => row.Action == action);

    [Fact]
    public void FocusOnAHotkeyFieldStartsRecordingAndLosingFocusEndsIt()
    {
        _model.HotkeyFieldFocused(Row(BarAction.Region));
        Assert.Equal(1, _hotkeys.RecordingsHeld);

        _model.HotkeyFieldLeft();
        Assert.Equal(0, _hotkeys.RecordingsHeld);
    }

    [Fact]
    public void AChordTheHookRecordsBecomesTheTextOfTheFocusedField()
    {
        Row(BarAction.Window).Chord = "Alt+PrintScreen";
        _model.HotkeyFieldFocused(Row(BarAction.Region));

        _hotkeys.Deliver(HotkeyChord.Parse("Shift+Win+S"));

        Assert.Equal("Win+Shift+S", Row(BarAction.Region).Chord);
        Assert.Equal("Alt+PrintScreen", Row(BarAction.Window).Chord);
    }

    [Fact]
    public void FocusMovingToAnotherFieldRecordsIntoThatFieldOnly()
    {
        _model.HotkeyFieldFocused(Row(BarAction.Region));
        _model.HotkeyFieldFocused(Row(BarAction.Record));
        Assert.Equal(1, _hotkeys.RecordingsHeld);

        _hotkeys.Deliver(HotkeyChord.Parse("Win+K"));

        Assert.Equal("Win+K", Row(BarAction.Record).Chord);
        Assert.NotEqual("Win+K", Row(BarAction.Region).Chord);
    }

    [Fact]
    public void ClosingTheWindowEndsTheRecording()
    {
        _model.HotkeyFieldFocused(Row(BarAction.Region));

        _model.ReleaseHotkeys();

        Assert.Equal(0, _hotkeys.RecordingsHeld);
    }

    [Fact]
    public void CancelEndsTheRecording()
    {
        _model.HotkeyFieldFocused(Row(BarAction.Region));

        _model.Cancel();

        Assert.Equal(0, _hotkeys.RecordingsHeld);
    }

    [Fact]
    public void ASaveEndsTheRecordingBeforeItRegistersTheNewChords()
    {
        _model.HotkeyFieldFocused(Row(BarAction.Region));
        int recordingsWhenRegistered = -1;
        _hotkeys.OnRegister = () => recordingsWhenRegistered = _hotkeys.RecordingsHeld;

        _model.Save();

        Assert.True(_model.Saved);
        Assert.Equal(0, recordingsWhenRegistered);
        Assert.Equal(0, _hotkeys.RecordingsHeld);
    }

    [Fact]
    public void LosingFocusWithoutEverGainingItRecordsNothing()
    {
        _model.HotkeyFieldLeft();

        Assert.Equal(0, _hotkeys.RecordCalls);
        Assert.Equal(0, _hotkeys.RecordingsHeld);
    }
}
