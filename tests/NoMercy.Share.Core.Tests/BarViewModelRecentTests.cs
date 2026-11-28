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
        bar.Recent.AskToDelete(bar.Recent.Rows[0]);
        return bar;
    }

    private async Task<BarViewModel> AskWithTwoLinks(FakeUploader host)
    {
        AddLink("a");
        AddLink("b");
        return await AskToDeleteTheNewest(host);
    }

    [Fact]
    public async Task DeleteAsksInTheRowAndDeletesNothingYet()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true };

        BarViewModel bar = await AskToDeleteTheNewest(host);

        RecentRowState state = bar.Recent.Rows[0].State;
        Assert.True(bar.IsRecentOpen);
        Assert.Null(bar.Ask);
        Assert.Equal(RecentRowStep.Asking, state.Step);
        Assert.Equal("Delete from the host?", state.Title);
        Assert.Equal("Cached up to 1 hour.", state.Detail);
        Assert.Equal("Delete", state.ConfirmLabel);
        Assert.Empty(host.Deleted);
        Assert.Single(_store.Load());
    }

    [Fact]
    public async Task AskingOnAnotherRowReturnsTheFirstToNormal()
    {
        BarViewModel bar = await AskWithTwoLinks(new FakeUploader { Deletes = true });

        bar.Recent.AskToDelete(bar.Recent.Rows[1]);

        Assert.Equal([RecentRowStep.Normal, RecentRowStep.Asking], bar.Recent.Rows.Select(row => row.State.Step));
    }

    [Fact]
    public async Task CancelKeepsTheLinkAndCallsNoHost()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true };
        BarViewModel bar = await AskToDeleteTheNewest(host);

        Assert.True(bar.Recent.ResetQuestions());

        Assert.Equal(RecentRowStep.Normal, bar.Recent.Rows[0].State.Step);
        Assert.False(bar.Recent.ResetQuestions());
        Assert.Empty(host.Deleted);
        Assert.Single(_store.Load());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClosingTheListReturnsTheQuestionToNormal(bool lightDismiss)
    {
        AddLink("a");
        BarViewModel bar = await AskToDeleteTheNewest(new FakeUploader { Deletes = true });

        if (lightDismiss)
        {
            bar.IsRecentOpen = false;
        }
        else
        {
            bar.CloseRecent();
        }

        await bar.RunAsync(BarAction.Recent);

        Assert.Equal(RecentRowStep.Normal, bar.Recent.Rows[0].State.Step);
    }

    [Fact]
    public async Task DeletingOffersNoAnswerAndDeletesOnlyOnce()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true, DeleteWaits = new TaskCompletionSource() };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        RecentRow row = bar.Recent.Rows[0];

        Task first = bar.Recent.DeleteAsync(row);
        Task second = bar.Recent.DeleteAsync(row);
        bar.Recent.AskToDelete(row);

        Assert.Equal(RecentRowStep.Deleting, row.State.Step);
        Assert.Equal("Deleting...", row.State.Title);
        Assert.False(row.State.OffersAnswer);
        host.DeleteWaits.SetResult();
        await Task.WhenAll(first, second);
        Assert.Single(host.Deleted);
    }

    [Fact]
    public async Task ADeletedRowLeavesAloneAndTheListStaysOpen()
    {
        string file = Path.Combine(_folder, "shot.png");
        File.WriteAllText(file, "made-up-picture-bytes");
        _store.Add(new RecentLink("https://share.example.test/exp-30d/a.png", RecentKind.Image, "image/png", 10, Now, null, file));
        FakeUploader host = new() { Deletes = true };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        List<string?> changed = [];
        bar.Recent.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        Assert.Equal("https://share.example.test/exp-30d/a.png", Assert.Single(host.Deleted).Url);
        Assert.Empty(_store.Load());
        Assert.True(File.Exists(file));
        Assert.True(bar.IsRecentOpen);
        Assert.True(bar.Recent.IsEmpty);
        Assert.Contains(nameof(RecentList.IsEmpty), changed);
        Assert.Null(bar.Notice);
    }

    [Fact]
    public async Task TheOtherRowsStayTheSameObjectsAfterADelete()
    {
        BarViewModel bar = await AskWithTwoLinks(new FakeUploader { Deletes = true });
        RecentRow other = bar.Recent.Rows[1];

        await bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        Assert.Same(other, Assert.Single(bar.Recent.Rows));
    }

    [Fact]
    public async Task ARefusedDeleteKeepsTheLinkAndSaysWhyInTheRow()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true, DeleteThrows = new ShareUploadException("the host said no") };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        RecentRow row = bar.Recent.Rows[0];

        await bar.Recent.DeleteAsync(row);

        Assert.Single(_store.Load());
        Assert.Equal(RecentRowStep.Failed, row.State.Step);
        Assert.Equal("Not deleted", row.State.Title);
        Assert.Equal("the host said no", row.State.Detail);
        Assert.Equal("Try again", row.State.ConfirmLabel);
        Assert.Null(bar.Notice);
        host.DeleteThrows = null;
        await bar.Recent.DeleteAsync(row);
        Assert.Empty(_store.Load());
    }

    [Fact]
    public async Task AnUnexpectedDeleteErrorShowsPlainWords()
    {
        AddLink("a");
        BarViewModel bar = await AskToDeleteTheNewest(
            new FakeUploader { Deletes = true, DeleteThrows = new InvalidOperationException("internal detail") });

        await bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        Assert.Equal("Something went wrong. The link is kept.", bar.Recent.Rows[0].State.Detail);
        Assert.Single(_store.Load());
    }

    [Fact]
    public async Task AFailureWhileTheListIsClosedShowsWhenItOpensAgain()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true, DeleteWaits = new TaskCompletionSource() };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        Task delete = bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        bar.CloseRecent();
        await bar.RunAsync(BarAction.Recent);
        Assert.Equal(RecentRowStep.Deleting, bar.Recent.Rows[0].State.Step);
        bar.CloseRecent();
        host.DeleteWaits.SetException(new ShareUploadException("the host said no"));
        await delete;
        await bar.RunAsync(BarAction.Recent);

        Assert.Equal(RecentRowStep.Failed, bar.Recent.Rows[0].State.Step);
        Assert.Equal("the host said no", bar.Recent.Rows[0].State.Detail);
    }

    [Fact]
    public async Task ARunningDeleteDoesNotBlockTheBar()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = true, DeleteWaits = new TaskCompletionSource() };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        Task delete = bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        await bar.RunAsync(BarAction.Screenshot);

        Assert.True(bar.IsScreenshotMenuOpen);
        host.DeleteWaits.SetResult();
        await delete;
    }

    [Fact]
    public async Task WhenNoProviderCanDeleteOnlyTheListEntryGoes()
    {
        AddLink("a");
        FakeUploader host = new() { Deletes = false };
        BarViewModel bar = await AskToDeleteTheNewest(host);
        RecentRowState state = bar.Recent.Rows[0].State;

        Assert.Equal("Only remove from the list?", state.Title);
        Assert.Equal("The upload stays online.", state.Detail);
        Assert.Equal("Remove", state.ConfirmLabel);
        await bar.Recent.DeleteAsync(bar.Recent.Rows[0]);

        Assert.Empty(host.Deleted);
        Assert.Empty(_store.Load());
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
        Assert.Equal(2, bar.Recent.Rows.Count);
        Assert.False(bar.Recent.IsEmpty);
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
        Assert.True(bar.Recent.IsEmpty);
        Assert.Empty(bar.Recent.Rows);
    }

    [Fact]
    public async Task TheListIsReadAgainEachTimeItOpens()
    {
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);
        bar.CloseRecent();

        AddLink("new");
        await bar.RunAsync(BarAction.Recent);

        Assert.Single(bar.Recent.Rows);
    }

    [Fact]
    public async Task TheStateFlagRaisesAChangeSoThePopupFollows()
    {
        BarViewModel bar = Bar();
        List<string?> changed = [];
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await bar.RunAsync(BarAction.Recent);

        Assert.Contains(nameof(BarViewModel.IsRecentOpen), changed);
    }

    [Fact]
    public async Task CopyPutsTheUrlOnTheClipboardSaysLinkCopiedAndClosesTheList()
    {
        AddLink("a");
        BarViewModel bar = Bar();
        await bar.RunAsync(BarAction.Recent);

        bar.CopyRecentCommand.Execute(bar.Recent.Rows[0]);

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

        bar.OpenRecentCommand.Execute(bar.Recent.Rows[0]);

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

        bar.OpenRecentCommand.Execute(bar.Recent.Rows[0]);

        Assert.Equal("That did not work. Try again.", bar.Notice);
    }

    [Fact]
    public async Task ARowIsMarkedExpiredWhenItsLinkHasExpired()
    {
        _store.Add(new RecentLink("https://share.example.test/exp-1d/old.png", RecentKind.Image, "image/png", 10, Now.AddDays(-2), Now.AddDays(-1), null));
        BarViewModel bar = Bar();

        await bar.RunAsync(BarAction.Recent);

        Assert.True(bar.Recent.Rows[0].IsExpired);
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
