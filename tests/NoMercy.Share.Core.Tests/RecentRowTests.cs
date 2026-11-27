using NoMercy.Share.Core.Recent;

namespace NoMercy.Share.Core.Tests;

public sealed class RecentRowTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-row-{Guid.NewGuid():N}");

    public RecentRowTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static RecentRow Row(TimeSpan age, TimeSpan? expiresIn = null, RecentKind kind = RecentKind.Image, string? file = null) =>
        new(new RecentLink("https://share.example.test/exp-30d/abc.png", kind, "image/png", 10, Now - age, expiresIn is { } left ? Now + left : null, file), Now);

    [Fact]
    public void ThePathHasNoHost()
    {
        Assert.Equal("exp-30d/abc.png", Row(TimeSpan.Zero).PathText);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(30, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(3 * 3600, "3 h ago")]
    [InlineData(2 * 86400, "2 d ago")]
    [InlineData(-120, "just now")]
    public void TheAgeIsShortInvariantEnglish(int seconds, string expected)
    {
        Assert.Equal(expected, Row(TimeSpan.FromSeconds(seconds)).AgeText);
    }

    [Theory]
    [InlineData(29 * 24, "expires in 29 days")]
    [InlineData(24, "expires in 1 day")]
    [InlineData(5, "expires in 5 h")]
    [InlineData(0, "expires in 1 min")]
    public void TheExpiryCountsDownInTheLargestWholeUnit(int hours, string expected)
    {
        Assert.Equal(expected, Row(TimeSpan.FromMinutes(1), TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(30)).ExpiryText);
    }

    [Fact]
    public void AnExpiredLinkSaysExpired()
    {
        RecentRow row = Row(TimeSpan.FromDays(31), TimeSpan.FromDays(-1));

        Assert.True(row.IsExpired);
        Assert.Equal("expired", row.ExpiryText);
    }

    [Fact]
    public void ALinkThatNeverExpiresHasNoExpiryText()
    {
        RecentRow row = Row(TimeSpan.FromMinutes(1));

        Assert.False(row.IsExpired);
        Assert.Null(row.ExpiryText);
    }

    [Fact]
    public void AnImageWithItsFileStillThereHasAThumbnail()
    {
        string file = Path.Combine(_folder, "shot.png");
        File.WriteAllBytes(file, [1]);

        Assert.Equal(file, Row(TimeSpan.Zero, file: file).ThumbnailPath);
    }

    [Fact]
    public void AnImageWhoseFileIsGoneHasNoThumbnail()
    {
        Assert.Null(Row(TimeSpan.Zero, file: Path.Combine(_folder, "gone.png")).ThumbnailPath);
    }

    [Fact]
    public void AVideoHasNoThumbnailEvenWithItsFile()
    {
        string file = Path.Combine(_folder, "clip.mp4");
        File.WriteAllBytes(file, [1]);

        RecentRow row = Row(TimeSpan.Zero, kind: RecentKind.Video, file: file);

        Assert.True(row.IsVideo);
        Assert.Null(row.ThumbnailPath);
    }
}
