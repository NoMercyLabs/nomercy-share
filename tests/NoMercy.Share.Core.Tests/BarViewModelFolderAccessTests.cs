using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class BarViewModelFolderAccessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"share-access-{Guid.NewGuid():N}");
    private readonly FakeScreenCapture _screen = new();
    private readonly FakeFolderAccess _access = new();

    public BarViewModelFolderAccessTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "not-a-folder"), "x");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string BlockedFolder => Path.Combine(_root, "not-a-folder", "caps");

    private string OpenFolder => Path.Combine(_root, "open");

    private CaptureFiles Files(string folder) => new(new CaptureSettings { Folder = folder }, "unused", () => DateTimeOffset.UnixEpoch, Path.Combine(_root, "fallback"));

    private BarViewModel Bar(CaptureFiles files) => new(
        new CaptureFlow(_screen, new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
        new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings { DefaultDays = 7 }, new ClipboardSettings()),
        files: files,
        folderAccess: _access);

    [Fact]
    public void ABlockedCapturesFolderWithProtectionOnShowsTheAsk()
    {
        _access.ProtectionOn = true;
        BarViewModel bar = Bar(Files(BlockedFolder));

        bar.CheckFolderAccess();

        Assert.Equal(Strings.Get("ask.folder", BlockedFolder), bar.Ask?.Text);
    }

    [Fact]
    public void ProtectionOffShowsNoAsk()
    {
        _access.ProtectionOn = false;
        BarViewModel bar = Bar(Files(BlockedFolder));

        bar.CheckFolderAccess();

        Assert.Null(bar.Ask?.Text);
    }

    [Fact]
    public void AWritableFolderShowsNoAskEvenWithProtectionOn()
    {
        _access.ProtectionOn = true;
        BarViewModel bar = Bar(Files(OpenFolder));

        bar.CheckFolderAccess();

        Assert.Null(bar.Ask?.Text);
    }

    [Fact]
    public async Task AllowThatWorksSaysTheCapturesSaveToTheFolderAgain()
    {
        _access.ProtectionOn = true;
        CaptureFiles files = Files(BlockedFolder);
        BarViewModel bar = Bar(files);
        bar.CheckFolderAccess();
        _access.OnRequest = () => File.Delete(Path.Combine(_root, "not-a-folder")); // Windows lifts the block

        await bar.AllowAccessAsync();

        Assert.Equal(1, _access.Requests);
        Assert.Null(bar.Ask?.Text);
        Assert.Equal(Strings.Get("ask.allowed", BlockedFolder), bar.Notice);
    }

    [Fact]
    public async Task AllowThatIsDeclinedSaysTheCapturesStayInTheFallbackFolder()
    {
        _access.ProtectionOn = true;
        BarViewModel bar = Bar(Files(BlockedFolder));
        bar.CheckFolderAccess();
        _access.Grants = false;

        await bar.AllowAccessAsync();

        Assert.Null(bar.Ask?.Text);
        Assert.Equal(Strings.Get("ask.declined", Path.Combine(_root, "fallback")), bar.Notice);
    }

    [Fact]
    public void NotNowHidesTheAskForTheRestOfTheRunWithoutAsking()
    {
        _access.ProtectionOn = true;
        BarViewModel bar = Bar(Files(BlockedFolder));
        bar.CheckFolderAccess();

        bar.DismissAccessAsk();
        bar.CheckFolderAccess();

        Assert.Null(bar.Ask?.Text);
        Assert.Equal(0, _access.Requests);
    }

    [Fact]
    public async Task ACaptureThatFellBackShowsTheAskOnce()
    {
        _access.ProtectionOn = true;
        CaptureFiles files = Files(BlockedFolder);
        BarViewModel bar = Bar(files);
        _screen.OnCapture = () => files.NewPath();

        await bar.RunAsync(BarAction.Window);

        Assert.Equal(Strings.Get("ask.folder", BlockedFolder), bar.Ask?.Text);
        bar.DismissAccessAsk();
        await bar.RunAsync(BarAction.Window);
        Assert.Null(bar.Ask?.Text);
    }

    [Fact]
    public async Task ACaptureThatFellBackShowsNoAskWhenProtectionIsOff()
    {
        _access.ProtectionOn = false;
        CaptureFiles files = Files(BlockedFolder);
        BarViewModel bar = Bar(files);
        _screen.OnCapture = () => files.NewPath();

        await bar.RunAsync(BarAction.Window);

        Assert.Null(bar.Ask?.Text);
    }

    [Fact]
    public void TheButtonsRunAllowAndNotNow()
    {
        _access.ProtectionOn = true;
        BarViewModel bar = Bar(Files(BlockedFolder));
        bar.CheckFolderAccess();

        bar.AskNoCommand.Execute(null);

        Assert.Null(bar.Ask?.Text);
    }

    private sealed class FakeFolderAccess : IFolderAccess
    {
        public bool ProtectionOn { get; set; }

        public bool Grants { get; set; } = true;

        public Action? OnRequest { get; set; }

        public int Requests { get; private set; }

        public bool IsProtectionOn() => ProtectionOn;

        public Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default)
        {
            Requests++;
            OnRequest?.Invoke();
            return Task.FromResult(Grants);
        }
    }
}
