using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelRecentTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-barrecent-{Guid.NewGuid():N}");
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeUrlOpener _opener = new();
    private readonly RecentLinks _store;

    public BarViewModelRecentTests()
    {
        Directory.CreateDirectory(_folder);
        _store = new RecentLinks(Path.Combine(_folder, "recent.json"), new RecentSettings(), () => Now);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private BarViewModel Bar(bool withStore = true, FakeUploader? host = null) => new(
        new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), _clipboard, new ExpirySettings(), new ClipboardSettings()),
        clock: () => Now,
        recent: withStore ? _store : null,
        opener: _opener,
        clipboard: _clipboard,
        deletion: host is null ? null : new ShareDeletion(() => [host]));

    private async Task<BarViewModel> AskToDeleteTheNewest(FakeUploader host)
    {
        BarViewModel bar = Bar(host: host);
        await bar.RunAsync(BarAction.Recent);
        bar.DeleteRecentCommand.Execute(bar.RecentRows[0]);
        return bar;
    }

    [Fact]
    public async Task DeleteAsksFirstAndDeletesNothingYet()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true };

        BarViewModel bar = await AskToDeleteTheNewest(host);

        Assert.False(bar.IsRecentOpen);
        Assert.NotNull(bar.Ask);
        Assert.Equal("Delete", bar.Ask.YesLabel);
        Assert.Equal("Cancel", bar.Ask.NoLabel);
        Assert.Empty(host.Deleted);
        Assert.Single(_store.Load());
    }

    [Fact]
    public async Task TheConfirmTextNamesTheBrowserCacheAndTheDiscordPreview()
    {
        AddLink("a");

        BarViewModel bar = await AskToDeleteTheNewest(new FakeUploader { Deletes = true });

        Assert.Contains("one hour", bar.Ask?.Text);
        Assert.Contains("Discord", bar.Ask?.Text);
        Assert.Contains("preview", bar.Ask?.Text);
    }

    [Fact]
    public async Task CancelKeepsTheLinkAndCallsNoHost()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true };
        BarViewModel bar = await AskToDeleteTheNewest(host);

        bar.AskNoCommand.Execute(null);

        Assert.Null(bar.Ask);
        Assert.Empty(host.Deleted);
        Assert.Single(_store.Load());
    }

    [Fact]
    public async Task YesDeletesFromTheHostAndTakesTheLinkOffTheListButKeepsTheFile()
    {
        string file = Path.Combine(_folder, "shot.png");
        File.WriteAllText(file, "made-up-picture-bytes");
        _store.Add(new RecentLink("https://share.example.test/exp-30d/a.png", RecentKind.Image, "image/png", 10, Now, null, file));
        FakeUploader host = new() { Deletes = true };
        BarViewModel bar = await AskToDeleteTheNewest(host);

        await bar.Ask!.Yes();

        Assert.Equal("https://share.example.test/exp-30d/a.png", Assert.Single(host.Deleted).Url);
        Assert.Empty(_store.Load());
        Assert.True(File.Exists(file));
        Assert.Null(bar.Ask);
        Assert.Equal("Link deleted", bar.Notice);
    }

    [Fact]
    public async Task ARefusedDeleteKeepsTheLinkAndSaysWhy()
    {
        AddLink("a");
        BarViewModel bar = await AskToDeleteTheNewest(
            new FakeUploader { Deletes = true, DeleteThrows = new ShareUploadException("the host said no") });

        await bar.Ask!.Yes();

        Assert.Single(_store.Load());
        Assert.Equal("Not deleted: the host said no. The link stays in the list.", bar.Notice);
    }

    [Fact]
    public async Task AnUnexpectedDeleteErrorKeepsTheLinkWithAPlainNotice()
    {
        AddLink("a");
        BarViewModel bar = await AskToDeleteTheNewest(
            new FakeUploader { Deletes = true, DeleteThrows = new InvalidOperationException("internal detail") });

        await bar.Ask!.Yes();

        Assert.Single(_store.Load());
        Assert.Equal("That did not work. The link stays in the list.", bar.Notice);
    }

    [Fact]
    public async Task WhenNoProviderCanDeleteOnlyTheListEntryGoes()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = false };
        BarViewModel bar = await AskToDeleteTheNewest(host);

        Assert.Contains("older Discord uploads", bar.Ask?.Text);
        await bar.Ask!.Yes();

        Assert.Empty(host.Deleted);
        Assert.Empty(_store.Load());
        Assert.Equal("Removed from the list", bar.Notice);
    }

    private void AddLink(string id) =>
        _store.Add(new RecentLink($"https://share.example.test/exp-30d/{id}.png", RecentKind.Image, "image/png", 10, Now.AddMinutes(-5), Now.AddDays(29), null));

    [Fact]
    public async Task TheRecentButtonOpensTheListWithTheStoredLinks()
    {
        AddLink("a");
        AddLink("b");
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);

        Assert.True(bar.IsRecentOpen);
        Assert.Equal(2, bar.RecentRows.Count);
        Assert.False(bar.IsRecentEmpty);
        Assert.Null(bar.Notice);
    }

    [Fact]
    public async Task APressOnTheOpenListClosesIt()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);
        await bar.RunAsync(BarAction.Recent);

        Assert.False(bar.IsRecentOpen);
    }

    [Fact]
    public async Task WithNoLinksTheListOpensEmpty()
    {
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);

        Assert.True(bar.IsRecentOpen);
        Assert.True(bar.IsRecentEmpty);
        Assert.Empty(bar.RecentRows);
    }

    [Fact]
    public async Task TheListIsReadAgainEachTimeItOpens()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);
        bar.CloseRecent();

        AddLink("new");
        await bar.RunAsync(BarAction.Recent);

        Assert.Single(bar.RecentRows);
    }

    [Fact]
    public async Task TheStateFlagRaisesAChangeSoThePopupFollows()
    {
        BarViewModel bar = Bar();
        List<string?> changed = [];
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await bar.RunAsync(BarAction.Recent);

        Assert.Contains(nameof(BarViewModel.IsRecentOpen), changed);
        Assert.Contains(nameof(BarViewModel.RecentRows), changed);
    }

    [Fact]
    public async Task CopyPutsTheUrlOnTheClipboardSaysLinkCopiedAndClosesTheList()
    {
        AddLink("a");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);

        bar.CopyRecentCommand.Execute(bar.RecentRows[0]);

        Assert.Equal(["https://share.example.test/exp-30d/a.png"], _clipboard.Texts);
        Assert.Equal("Link copied", bar.Notice);
        Assert.False(bar.IsRecentOpen);
    }

    [Fact]
    public async Task OpenHandsTheUrlToTheOpenerAndClosesTheList()
    {
        AddLink("a");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);

        bar.OpenRecentCommand.Execute(bar.RecentRows[0]);

        Assert.Equal([new Uri("https://share.example.test/exp-30d/a.png")], _opener.Opened);
        Assert.False(bar.IsRecentOpen);
    }

    [Fact]
    public async Task AnOpenerThatRefusesBecomesANoticeNotACrash()
    {
        AddLink("a");
        _opener.Throws = new ArgumentException("not https");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);

        bar.OpenRecentCommand.Execute(bar.RecentRows[0]);

        Assert.Equal("That did not work. Try again.", bar.Notice);
    }

    [Fact]
    public async Task ARowIsMarkedExpiredWhenItsLinkHasExpired()
    {
        _store.Add(new RecentLink("https://share.example.test/exp-1d/old.png", RecentKind.Image, "image/png", 10, Now.AddDays(-2), Now.AddDays(-1), null));
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);

        Assert.True(bar.RecentRows[0].IsExpired);
    }

    [Fact]
    public async Task WithNoStoreTheRecentButtonSaysItIsComingLater()
    {
        BarViewModel bar = Bar(withStore: false);

        await bar.RunAsync(BarAction.Recent);

        Assert.Equal("Coming in a later step", bar.Notice);
        Assert.False(bar.IsRecentOpen);
    }
}
