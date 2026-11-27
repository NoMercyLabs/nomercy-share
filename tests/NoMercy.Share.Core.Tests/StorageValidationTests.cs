using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class StorageValidationTests
{
    private static StorageSettings Good() => new()
    {
        Endpoint = "https://account.r2.example.test",
        Bucket = "share",
        PublicBaseUrl = "https://share.example.test",
        CredentialName = "nomercy-share-r2"
    };

    [Fact]
    public void CompleteSettingsPass()
    {
        Good().Validate();
    }

    [Theory]
    [InlineData("Endpoint")]
    [InlineData("Bucket")]
    [InlineData("PublicBaseUrl")]
    [InlineData("CredentialName")]
    [InlineData("Region")]
    public void AMissingValueNamesTheSetting(string name)
    {
        StorageSettings settings = name switch
        {
            "Endpoint" => Good() with { Endpoint = "" },
            "Bucket" => Good() with { Bucket = " " },
            "PublicBaseUrl" => Good() with { PublicBaseUrl = "" },
            "CredentialName" => Good() with { CredentialName = "" },
            _ => Good() with { Region = "" }
        };

        InvalidStorageSettingsException error = Assert.Throws<InvalidStorageSettingsException>(settings.Validate);

        Assert.Equal($"Storage.{name}", error.Setting);
        Assert.Contains($"Storage.{name}", error.Message);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://host")]
    public void AnAddressThatIsNotHttpFails(string address)
    {
        InvalidStorageSettingsException error = Assert.Throws<InvalidStorageSettingsException>(() => (Good() with { Endpoint = address }).Validate());

        Assert.Contains("Storage.Endpoint", error.Message);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(500)]
    public void AKeyLengthOutsideTheLimitsFails(int length)
    {
        InvalidStorageSettingsException error = Assert.Throws<InvalidStorageSettingsException>(() => (Good() with { KeyLength = length }).Validate());

        Assert.Contains("Storage.KeyLength", error.Message);
    }

    [Fact]
    public void ABucketListWithANonPositiveDayFails()
    {
        InvalidStorageSettingsException error = Assert.Throws<InvalidStorageSettingsException>(() => (Good() with { ExpiryBucketDays = [7, 0] }).Validate());

        Assert.Contains("Storage.ExpiryBucketDays", error.Message);
    }

    [Fact]
    public void AnEmptyBucketListFails()
    {
        Assert.Throws<InvalidStorageSettingsException>(() => (Good() with { ExpiryBucketDays = [] }).Validate());
    }

    [Fact]
    public void ASettingsFileKeepsTheNewStorageValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"share-test-{Guid.NewGuid():N}", "settings.json");
        SettingsFile file = new(path);

        file.Save(new ShareSettings { Storage = Good() with { KeyLength = 12, ExpiryBucketDays = [2, 9] }, Clipboard = new ClipboardSettings { Content = ClipboardContent.Both } });
        ShareSettings read = file.Load();

        Assert.Equal(12, read.Storage.KeyLength);
        Assert.Equal([2, 9], read.Storage.ExpiryBucketDays);
        Assert.Equal(ClipboardContent.Both, read.Clipboard.Content);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void TheClipboardDefaultIsTheLink()
    {
        Assert.Equal(ClipboardContent.Link, new ClipboardSettings().Content);
    }
}
