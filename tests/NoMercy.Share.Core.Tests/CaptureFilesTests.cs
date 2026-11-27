using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class CaptureFilesTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 27, 15, 30, 45, 123, TimeSpan.Zero);

    [Fact]
    public void TheDefaultFileNameIsUtcAndSortable()
    {
        Assert.Equal("nomercy-share-20261127T153045123Z.png", CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, Moment));
    }

    [Fact]
    public void ALocalTimeIsWrittenAsUtc()
    {
        DateTimeOffset local = new(2026, 11, 27, 17, 30, 45, 123, TimeSpan.FromHours(2));

        Assert.Equal("nomercy-share-20261127T153045123Z.png", CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, local));
    }

    [Fact]
    public void LaterCapturesSortAfterEarlierOnes()
    {
        string first = CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, Moment);
        string second = CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, Moment.AddMilliseconds(1));
        string nextYear = CaptureFiles.FileName(ShareDefaults.CaptureFileNamePattern, Moment.AddYears(1));

        Assert.True(string.CompareOrdinal(first, second) < 0);
        Assert.True(string.CompareOrdinal(second, nextYear) < 0);
    }

    [Fact]
    public void ACharacterThatCannotLiveInAFileNameBecomesADash()
    {
        string name = CaptureFiles.FileName("a:b/c?{0:yyyy}", Moment);

        Assert.Equal("a-b-c-2026.png", name);
    }

    [Fact]
    public void TheFolderDefaultsToNoMercyShareUnderPictures()
    {
        Assert.Equal(Path.Combine("pictures", "NoMercy Share"), CaptureFiles.ResolveFolder(null, "pictures"));
        Assert.Equal(Path.Combine("pictures", "NoMercy Share"), CaptureFiles.ResolveFolder("  ", "pictures"));
    }

    [Fact]
    public void TheFolderSettingBeatsTheDefault()
    {
        Assert.Equal("other", CaptureFiles.ResolveFolder("other", "pictures"));
    }

    [Fact]
    public void ANewPathCreatesTheFolderAndNeverReusesAFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-files-{Guid.NewGuid():N}");
        try
        {
            CaptureFiles files = new(new CaptureSettings { Folder = Path.Combine(root, "caps") }, "unused", () => Moment);

            string first = files.NewPath();
            File.WriteAllText(first, "x");
            string second = files.NewPath();

            Assert.Equal(Path.Combine(root, "caps", "nomercy-share-20261127T153045123Z.png"), first);
            Assert.NotEqual(first, second);
            Assert.Equal(Path.Combine(root, "caps"), Path.GetDirectoryName(second));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TheCaptureSettingsHaveNamedDefaults()
    {
        CaptureSettings settings = new();

        Assert.Null(settings.Folder);
        Assert.Equal(ShareDefaults.CaptureFileNamePattern, settings.FileNamePattern);
        Assert.Equal(ShareDefaults.CaptureHideDelayMilliseconds, settings.HideDelayMilliseconds);
        Assert.Equal(FullScreenArea.CursorMonitor, settings.FullScreenArea);
        Assert.Equal(new CaptureSettings(), new ShareSettings().Capture);
    }

    [Fact]
    public void ACaptureFolderInSettingsJsonIsRead()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-settings-{Guid.NewGuid():N}", "settings.json");
        try
        {
            SettingsFile file = new(path);
            file.Save(new ShareSettings { Capture = new CaptureSettings { Folder = "mine", FullScreenArea = FullScreenArea.VirtualScreen } });

            ShareSettings loaded = file.Load();

            Assert.Equal("mine", loaded.Capture.Folder);
            Assert.Equal(FullScreenArea.VirtualScreen, loaded.Capture.FullScreenArea);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
