using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class BarPositionStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-pos-{Guid.NewGuid():N}");

    private SettingsFile File => new(Path.Combine(_folder, "settings.json"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ANewInstallHasNoSavedPosition()
    {
        Assert.Null(new BarPositionStore(File).Load());
    }

    [Fact]
    public void ASavedPositionIsRestoredByAnotherStoreOnTheSameFile()
    {
        new BarPositionStore(File).Save(new BarPosition(320.5, 48));

        Assert.Equal(new BarPosition(320.5, 48), new BarPositionStore(File).Load());
    }

    [Fact]
    public void SavingThePositionKeepsTheOtherSettings()
    {
        File.Save(new ShareSettings { Storage = new StorageSettings { Bucket = "kept" } });

        new BarPositionStore(File).Save(new BarPosition(1, 2));

        Assert.Equal("kept", File.Load().Storage.Bucket);
    }
}
