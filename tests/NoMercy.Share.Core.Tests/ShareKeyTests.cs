using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class ShareKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private static SharePlan Plan(Expiry expiry, StorageSettings? settings = null, string file = "/home/someone/Private Name.PNG") =>
        new ShareKeyPlanner(settings ?? new StorageSettings()).Plan(expiry, Now, file);

    [Fact]
    public void TheIdHasTheConfiguredLengthAndOnlyUrlSafeCharacters()
    {
        string key = Plan(Expiry.InDays(7), new StorageSettings { KeyLength = 14 }).Key;
        string id = Path.GetFileNameWithoutExtension(key);

        Assert.Equal(14, id.Length);
        Assert.All(id, c => Assert.Contains(c, ShareKeyPlanner.KeyAlphabet));
    }

    [Fact]
    public void TheKeyNeverHoldsTheLocalFileNameButKeepsTheExtension()
    {
        string key = Plan(Expiry.InDays(7)).Key;

        Assert.DoesNotContain("Private", key);
        Assert.DoesNotContain("someone", key);
        Assert.EndsWith(".png", key);
    }

    [Fact]
    public void ADangerousExtensionIsDropped()
    {
        Assert.DoesNotContain('.', Plan(Expiry.Never, file: "/x/a.p?n g").Key);
        Assert.DoesNotContain('.', Plan(Expiry.Never, file: "/x/noextension").Key);
    }

    [Fact]
    public void TwoPlansGetDifferentIds()
    {
        Assert.NotEqual(Plan(Expiry.Never).Key, Plan(Expiry.Never).Key);
    }

    [Theory]
    [InlineData(1, "exp-1d", 1)]
    [InlineData(2, "exp-7d", 7)]
    [InlineData(7, "exp-7d", 7)]
    [InlineData(30, "exp-30d", 30)]
    [InlineData(31, "exp-90d", 90)]
    [InlineData(400, "exp-90d", 90)]
    public void ALifetimeGoesUnderTheSmallestPrefixThatCoversIt(int days, string prefix, int deletedAfterDays)
    {
        SharePlan plan = Plan(Expiry.InDays(days));

        Assert.StartsWith(prefix + "/", plan.Key);
        Assert.Equal(Now.AddDays(deletedAfterDays), plan.ExpiresAt);
    }

    [Fact]
    public void NeverGoesUnderTheKeepPrefixWithoutAnExpiry()
    {
        SharePlan plan = Plan(Expiry.Never, new StorageSettings { KeepPrefix = "forever" });

        Assert.StartsWith("forever/", plan.Key);
        Assert.Null(plan.ExpiresAt);
    }

    [Fact]
    public void ThePrefixPatternAndTheBucketsAreSettings()
    {
        SharePlan plan = Plan(Expiry.InDays(3), new StorageSettings { ExpiryPrefixPattern = "ttl/{0}", ExpiryBucketDays = [5, 20] });

        Assert.StartsWith("ttl/5/", plan.Key);
    }

    [Fact]
    public void TheSidecarSitsNextToTheObject()
    {
        SharePlan plan = Plan(Expiry.InDays(7), new StorageSettings { SidecarSuffix = ".meta.json" });

        Assert.Equal(plan.Key + ".meta.json", plan.SidecarKey);
    }

    [Fact]
    public void TheLinkIsThePublicBaseUrlPlusTheKeyWhateverTheSlashes()
    {
        Assert.Equal("https://share.example.test/exp-7d/abc.png", new StorageSettings { PublicBaseUrl = "https://share.example.test" }.LinkFor("exp-7d/abc.png").AbsoluteUri);
        Assert.Equal("https://share.example.test/s/exp-7d/abc.png", new StorageSettings { PublicBaseUrl = "https://share.example.test/s/" }.LinkFor("exp-7d/abc.png").AbsoluteUri);
    }
}
