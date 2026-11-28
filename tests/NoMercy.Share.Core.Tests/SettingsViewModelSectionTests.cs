using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class SettingsViewModelSectionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-section-test-{Guid.NewGuid():N}");
    private readonly SettingsViewModel _model;
    private readonly SettingsFile _file;
    private readonly LiveSettings _live;

    public SettingsViewModelSectionTests()
    {
        _file = new SettingsFile(SettingsFile.DefaultPath(_folder));
        _live = new LiveSettings(_file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        _model = new SettingsViewModel(_file, _live, new FakeCredentialStore(), new HotkeyBinder(new FakeGlobalHotkeys(), bar, _live.Current.Hotkeys), ["r2", "s3"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheWindowOpensOnGeneralAndOnlyTheSelectedSectionIsShown()
    {
        Assert.Equal(SettingsSection.General, _model.SelectedSection);
        Assert.True(_model.ShowsGeneral);
        Assert.False(_model.ShowsSharing);

        List<string?> changed = [];
        _model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _model.SelectedSection = SettingsSection.Recording;

        Assert.True(_model.ShowsRecording);
        Assert.False(_model.ShowsSharing);
        Assert.False(_model.ShowsHotkeys);
        Assert.Contains(nameof(SettingsViewModel.ShowsSharing), changed);
        Assert.Contains(nameof(SettingsViewModel.ShowsRecording), changed);
    }

    [Fact]
    public void ShowBarInCapturesIsSavedAndASaveThatTouchesNothingKeepsIt()
    {
        _model.ShowBarInCaptures = true;

        _model.Save();

        Assert.True(_model.Saved);
        Assert.True(_file.Load().Bar.ShowInCaptures);
        Assert.True(_live.Current.Bar.ShowInCaptures);
    }

    [Fact]
    public void AHotkeyRowListensWhileItsFieldHasFocusAndStopsWhenItIsLeft()
    {
        HotkeyRow row = _model.Hotkeys[0];

        _model.HotkeyFieldFocused(row);
        Assert.True(row.IsListening);

        _model.HotkeyFieldLeft();
        Assert.False(row.IsListening);
    }

    [Fact]
    public void ASaveWithAMistakeInAnotherSectionJumpsToTheFirstSectionWithAnError()
    {
        _model.SelectedSection = SettingsSection.Hotkeys;
        _model.FrameRate = "abc";

        _model.Save();

        Assert.False(_model.Saved);
        Assert.Equal(SettingsSection.Recording, _model.SelectedSection);

        _model.MaxSizeMegabytes = "abc";
        _model.Save();

        Assert.Equal(SettingsSection.General, _model.SelectedSection);
    }
}
