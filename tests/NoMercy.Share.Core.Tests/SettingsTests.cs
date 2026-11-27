using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void ADefaultExpiryIsThirtyDays()
    {
        Assert.Equal(Expiry.InDays(30), new ExpirySettings().Default);
    }

    [Fact]
    public void ANullDefaultMeansLinksNeverExpire()
    {
        Assert.True(new ExpirySettings { DefaultDays = null }.Default.IsNever);
    }

    [Fact]
    public void TheDefaultExpiryIsASetting()
    {
        Assert.Equal(Expiry.InDays(7), new ExpirySettings { DefaultDays = 7 }.Default);
    }

    [Fact]
    public void AnUploadOwnChoiceBeatsTheDefault()
    {
        ExpirySettings settings = new() { DefaultDays = 7 };

        Assert.Equal(Expiry.Never, settings.Resolve(Expiry.Never));
        Assert.Equal(Expiry.InDays(1), settings.Resolve(Expiry.InDays(1)));
        Assert.Equal(Expiry.InDays(7), settings.Resolve(null));
    }

    [Fact]
    public void TheDefaultProviderIsR2()
    {
        Assert.Equal("r2", new StorageSettings().Provider);
    }

    [Fact]
    public void TheProviderIsPickedByNameFromTheSettings()
    {
        FakeUploader r2 = new("r2");
        FakeUploader other = new("minio");

        Assert.Same(other, ShareUploaderSelector.Select([r2, other], new StorageSettings { Provider = "minio" }));
        Assert.Same(r2, ShareUploaderSelector.Select([r2, other], new StorageSettings()));
    }

    [Fact]
    public void TheProviderNameIgnoresCase()
    {
        FakeUploader r2 = new("r2");

        Assert.Same(r2, ShareUploaderSelector.Select([r2], new StorageSettings { Provider = "R2" }));
    }

    [Fact]
    public void AnUnknownProviderFailsAndNamesTheKnownOnes()
    {
        UnknownStorageProviderException error = Assert.Throws<UnknownStorageProviderException>(
            () => ShareUploaderSelector.Select([new FakeUploader("r2")], new StorageSettings { Provider = "nope" }));

        Assert.Contains("nope", error.Message);
        Assert.Contains("r2", error.Message);
    }

    [Fact]
    public void ASettingsFileKeepsEveryValueItWrote()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-test-{Guid.NewGuid():N}", "settings.json");
        SettingsFile file = new(path);
        ShareSettings written = new()
        {
            Storage = new StorageSettings { Provider = "minio", Bucket = "b", CredentialName = "share-cred" },
            Expiry = new ExpirySettings { DefaultDays = null }
        };

        file.Save(written);
        ShareSettings read = file.Load();

        Assert.Equal("minio", read.Storage.Provider);
        Assert.Equal("share-cred", read.Storage.CredentialName);
        Assert.Null(read.Expiry.DefaultDays);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void AMissingSettingsFileGivesTheDefaults()
    {
        ShareSettings read = new SettingsFile(Path.Combine(Path.GetTempPath(), $"share-none-{Guid.NewGuid():N}.json")).Load();

        Assert.Equal(new ShareSettings().Storage, read.Storage);
        Assert.Equal(30, read.Expiry.DefaultDays);
    }
}
