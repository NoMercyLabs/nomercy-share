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

    public SettingsViewModelSectionTests()
    {
        SettingsFile file = new(SettingsFile.DefaultPath(_folder));
        LiveSettings live = new(file.Load());
        BarViewModel bar = new(
            new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
            new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()));
        _model = new SettingsViewModel(file, live, new FakeCredentialStore(), new HotkeyBinder(new FakeGlobalHotkeys(), bar, live.Current.Hotkeys), ["r2", "s3"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheWindowOpensOnSharingAndOnlyTheSelectedSectionIsShown()
    {
        Assert.Equal(SettingsSection.Sharing, _model.SelectedSection);
        Assert.True(_model.ShowsSharing);

        List<string?> changed = [];
        _model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _model.SelectedSection = SettingsSection.Recording;

        Assert.True(_model.ShowsRecording);
        Assert.False(_model.ShowsSharing);
        Assert.False(_model.ShowsLinks);
        Assert.False(_model.ShowsHotkeys);
        Assert.Contains(nameof(SettingsViewModel.ShowsSharing), changed);
        Assert.Contains(nameof(SettingsViewModel.ShowsRecording), changed);
    }

    [Fact]
    public void ASaveWithAMistakeInAnotherSectionJumpsToTheFirstSectionWithAnError()
    {
        _model.SelectedSection = SettingsSection.Links;
        _model.MaxSizeMegabytes = "abc";

        _model.Save();

        Assert.False(_model.Saved);
        Assert.Equal(SettingsSection.Recording, _model.SelectedSection);
    }
}
