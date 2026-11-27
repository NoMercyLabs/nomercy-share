using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class RecentLinksTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-recent-{Guid.NewGuid():N}");
    private readonly DateTimeOffset _now = Now;

    public RecentLinksTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string FilePath => Path.Combine(_folder, "recent.json");

    private RecentLinks Store(RecentSettings? settings = null) => new(FilePath, settings ?? new RecentSettings(), () => _now);

    private static RecentLink Link(string id, DateTimeOffset created, DateTimeOffset? expires = null) =>
        new($"https://share.example.test/exp-30d/{id}.png", RecentKind.Image, "image/png", 1234, created, expires, null);

    private static string[] Ids(IEnumerable<RecentLink> links) => [.. links.Select(link => Path.GetFileNameWithoutExtension(link.Url))];

    [Fact]
    public void TheDefaultsAreFiftyEntriesAndSevenDaysOfExpiredLinks()
    {
        RecentSettings settings = new();

        Assert.Equal(50, settings.MaxEntries);
        Assert.Equal(7, settings.KeepExpiredDays);
        Assert.Equal(50, new ShareSettings().Recent.MaxEntries);
    }

    [Fact]
    public void TheRecentSettingsRoundTripThroughSettingsJson()
    {
        SettingsFile file = new(Path.Combine(_folder, "settings.json"));
        file.Save(new ShareSettings { Recent = new RecentSettings { MaxEntries = 9, KeepExpiredDays = 2 } });

        RecentSettings loaded = file.Load().Recent;

        Assert.Equal(9, loaded.MaxEntries);
        Assert.Equal(2, loaded.KeepExpiredDays);
    }

    [Fact]
    public void ANewStoreIsEmpty()
    {
        Assert.Empty(Store().Load());
    }

    [Fact]
    public void AnAddedLinkIsListedAndSurvivesANewStoreOnTheSameFile()
    {
        Store().Add(Link("a", Now));

        RecentLink loaded = Assert.Single(Store().Load());

        Assert.Equal("https://share.example.test/exp-30d/a.png", loaded.Url);
        Assert.Equal(RecentKind.Image, loaded.Kind);
        Assert.Equal(1234, loaded.SizeBytes);
        Assert.Equal(Now, loaded.CreatedAt);
    }

    [Fact]
    public void TheNewestLinkComesFirst()
    {
        RecentLinks store = Store();
        store.Add(Link("old", Now.AddMinutes(-10)));
        store.Add(Link("mid", Now.AddMinutes(-5)));
        store.Add(Link("new", Now));

        Assert.Equal(["new", "mid", "old"], Ids(store.Load()));
    }

    [Fact]
    public void OnlyTheNewestMaxEntriesAreKept()
    {
        RecentLinks store = Store(new RecentSettings { MaxEntries = 3 });
        for (int i = 0; i < 5; i++)
        {
            store.Add(Link($"l{i}", Now.AddMinutes(i)));
        }

        Assert.Equal(["l4", "l3", "l2"], Ids(store.Load()));
        Assert.Equal(3, Store(new RecentSettings { MaxEntries = 3 }).Load().Count);
    }

    [Fact]
    public void AnExpiredLinkStaysListedAndIsMarkedExpired()
    {
        RecentLinks store = Store();
        store.Add(Link("gone", Now.AddDays(-31), Now.AddDays(-1)));
        store.Add(Link("live", Now.AddMinutes(1), Now.AddDays(29)));
        store.Add(Link("keep", Now.AddMinutes(2), null));

        IReadOnlyList<RecentLink> links = store.Load();

        Assert.Equal(["keep", "live", "gone"], Ids(links));
        Assert.Equal([false, false, true], links.Select(link => link.IsExpired(_now)));
    }

    [Fact]
    public void AnExpiredLinkDropsOffAfterTheKeepExpiredDays()
    {
        RecentLinks store = Store();
        store.Add(Link("six", Now.AddDays(-36), Now.AddDays(-6)));
        store.Add(Link("eight", Now.AddDays(-38), Now.AddDays(-8)));

        Assert.Equal(["six"], Ids(store.Load()));
    }

    [Fact]
    public void KeepExpiredDaysIsASetting()
    {
        Store().Add(Link("two", Now.AddDays(-32), Now.AddDays(-2)));

        Assert.Single(Store(new RecentSettings { KeepExpiredDays = 7 }).Load());
        Assert.Empty(Store(new RecentSettings { KeepExpiredDays = 1 }).Load());
    }

    [Fact]
    public void ACorruptFileStartsEmptyAndKeepsABackupOfTheBadFile()
    {
        File.WriteAllText(FilePath, "{ this is not json");

        Assert.Empty(Store().Load());
        Assert.Equal("{ this is not json", File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void AfterACorruptFileTheNextAddStartsAFreshList()
    {
        File.WriteAllText(FilePath, "[[[");
        RecentLinks store = Store();

        store.Add(Link("fresh", Now));

        Assert.Single(Store().Load());
        Assert.Equal("[[[", File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void ASaveLeavesNoTemporaryFileAndAValidJsonFile()
    {
        Store().Add(Link("a", Now));

        Assert.False(File.Exists(FilePath + ".tmp"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(FilePath));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [Fact]
    public void AFailedWriteLeavesTheOldFileWhole()
    {
        RecentLinks store = Store();
        store.Add(Link("a", Now));
        string before = File.ReadAllText(FilePath);
        Directory.CreateDirectory(FilePath + ".tmp");

        Assert.ThrowsAny<Exception>(() => store.Add(Link("b", Now)));

        Assert.Equal(before, File.ReadAllText(FilePath));
    }

    [Fact]
    public void AStaleTemporaryFileFromACrashedWriteDoesNotBreakTheNextSave()
    {
        File.WriteAllText(FilePath + ".tmp", "half a wri");
        RecentLinks store = Store();

        store.Add(Link("a", Now));

        Assert.Single(store.Load());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task ASharedCaptureIsRecordedWithItsLinkTypeSizeAndExpiry()
    {
        string file = Path.Combine(_folder, "shot.png");
        File.WriteAllBytes(file, [1, 2, 3]);
        DateTimeOffset expires = Now.AddDays(30);
        RecentLinks store = Store();
        ShareFlow flow = new(new ExpiringUploader(expires), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings(), store);

        await flow.ShareAsync(new CaptureResult(file, "image/png", 10, 10, 3));

        RecentLink recorded = Assert.Single(store.Load());
        Assert.Equal("https://share.example.test/exp-30d/abc.png", recorded.Url);
        Assert.Equal(RecentKind.Image, recorded.Kind);
        Assert.Equal("image/png", recorded.ContentType);
        Assert.Equal(3, recorded.SizeBytes);
        Assert.Equal(Now, recorded.CreatedAt);
        Assert.Equal(expires, recorded.ExpiresAt);
        Assert.Equal(file, recorded.FilePath);
    }

    [Fact]
    public async Task AVideoIsRecordedAsAVideoAndAMissingFileIsNotKept()
    {
        RecentLinks store = Store();
        ShareFlow flow = new(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings(), store);

        await flow.ShareAsync(new CaptureResult(Path.Combine(_folder, "gone.mp4"), "video/mp4", SizeBytes: 5000));

        RecentLink recorded = Assert.Single(store.Load());
        Assert.Equal(RecentKind.Video, recorded.Kind);
        Assert.Null(recorded.FilePath);
        Assert.Null(recorded.ExpiresAt);
    }

    [Fact]
    public async Task ALocalOnlyCaptureIsNotRecorded()
    {
        RecentLinks store = Store();
        ShareFlow flow = new(new FakeUploader { IsConfigured = false }, new FakeClipboard(), new ExpirySettings(), new ClipboardSettings(), store);

        await flow.ShareAsync(new CaptureResult("/tmp/shot.png", "image/png"));

        Assert.Empty(store.Load());
    }

    [Fact]
    public async Task AFailedUploadIsNotRecorded()
    {
        RecentLinks store = Store();
        ShareFlow flow = new(new FakeUploader { Throws = new ShareUploadException("down") }, new FakeClipboard(), new ExpirySettings(), new ClipboardSettings(), store);

        await Assert.ThrowsAsync<ShareUploadException>(() => flow.ShareAsync(new CaptureResult("/tmp/shot.png", "image/png")));

        Assert.Empty(store.Load());
    }

    [Fact]
    public async Task AListThatCannotBeWrittenDoesNotFailTheShare()
    {
        RecentLinks store = Store();
        Directory.CreateDirectory(FilePath + ".tmp");
        FakeClipboard clipboard = new();
        ShareFlow flow = new(new FakeUploader(), clipboard, new ExpirySettings(), new ClipboardSettings(), store);

        ShareOutcome outcome = await flow.ShareAsync(new CaptureResult("/tmp/shot.png", "image/png"));

        Assert.NotNull(outcome.Link);
        Assert.Single(clipboard.Texts);
    }

    private sealed class ExpiringUploader(DateTimeOffset expires) : IShareUploader
    {
        public string ProviderName => "r2";

        public bool IsConfigured => true;

        public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShareLink(new Uri("https://share.example.test/exp-30d/abc.png"), expires));
    }
}
