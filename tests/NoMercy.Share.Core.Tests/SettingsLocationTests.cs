using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class SettingsLocationTests
{
    [Fact]
    public void ADataFolderOverridePutsSettingsInThatFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "share-data-test");

        Assert.Equal(Path.Combine(folder, "settings.json"), SettingsFile.DefaultPath(folder));
    }

    [Fact]
    public void RecentLinksFollowTheDataFolderOverride()
    {
        string folder = Path.Combine(Path.GetTempPath(), "share-data-test");

        Assert.Equal(Path.Combine(folder, "recent.json"), RecentLinks.DefaultPath(folder));
    }

    [Fact]
    public void WithoutAnOverrideSettingsLiveInTheUsersAppDataFolder()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.Equal(Path.Combine(appData, "NoMercy Share", "settings.json"), SettingsFile.DefaultPath(null));
    }

    [Fact]
    public void TheOverrideIsReadFromItsOwnVariable()
    {
        Assert.Equal("NOMERCY_SHARE_DATA", SettingsFile.DataFolderVariable);
    }
}
