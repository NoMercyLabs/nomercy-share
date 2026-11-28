using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

internal sealed class FakeScreenCapture : IScreenCapture
{
    public List<string> Calls { get; } = [];

    public Exception? Throws { get; set; }

    /// <summary>Runs inside every capture, so a test can look at the state at that moment.</summary>
    public Action? OnCapture { get; set; }

    /// <summary>When set, an area capture waits for it, as the real capture waits for the overlay.</summary>
    public IRegionSelector? Picker { get; set; }

    public async Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default)
    {
        Task<CaptureResult> shot = Run("region");
        if (Picker is not null)
        {
            await FakeRegionSelector.PickAsync(Picker, cancellationToken);
        }

        return await shot;
    }

    public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) => Run("window");

    public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) => Run("fullscreen");

    private Task<CaptureResult> Run(string call)
    {
        Calls.Add(call);
        OnCapture?.Invoke();
        return Throws is null ? Task.FromResult(new CaptureResult($"/tmp/{call}.png", "image/png")) : Task.FromException<CaptureResult>(Throws);
    }
}

internal sealed class FakeScreenRecorder : IScreenRecorder
{
    public bool IsRecording { get; private set; }

    public Exception? StartThrows { get; set; }

    public Exception? StopThrows { get; set; }

    public CaptureResult Result { get; set; } = new("/tmp/recording.mp4", "video/mp4");

    public string? StartNotice { get; set; }

    public event EventHandler<string>? Failed;

    /// <summary>Ends the recording the way a dying ffmpeg does: no stop call, only the event.</summary>
    public void Fail(string message)
    {
        IsRecording = false;
        Failed?.Invoke(this, message);
    }

    public RecordingOptions? Options { get; private set; }

    /// <summary>Runs inside every start, so a test can look at the state at that moment.</summary>
    public Action? OnStart { get; set; }

    /// <summary>When set, an area recording waits for it before it starts.</summary>
    public IRegionSelector? Picker { get; set; }

    public async Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        Options = options;
        OnStart?.Invoke();
        if (StartThrows is not null)
        {
            throw StartThrows;
        }

        if (Picker is not null && options.Source == CaptureKind.Region)
        {
            await FakeRegionSelector.PickAsync(Picker, cancellationToken);
        }

        IsRecording = true;
    }

    public Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default)
    {
        IsRecording = false;
        return StopThrows is null
            ? Task.FromResult(Result)
            : Task.FromException<CaptureResult>(StopThrows);
    }
}

internal sealed class FakeWebPageCapture : IWebPageCapture
{
    public List<WebCaptureRequest> Requests { get; } = [];

    public Exception? Throws { get; set; }

    public Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Throws is null ? Task.FromResult(new CaptureResult("/tmp/page.png", "image/png")) : Task.FromException<CaptureResult>(Throws);
    }
}

internal sealed class FakeUploader(string name = "r2") : IShareUploader
{
    public string ProviderName { get; } = name;

    public List<(string File, Expiry Expiry)> Uploads { get; } = [];

    public Exception? Throws { get; set; }

    public bool IsConfigured { get; set; } = true;

    public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default)
    {
        if (Throws is not null)
        {
            return Task.FromException<ShareLink>(Throws);
        }

        Uploads.Add((filePath, expiry));
        return Task.FromResult(new ShareLink(new Uri("https://share.example.test/abc"), null));
    }

    public bool Deletes { get; set; }

    public Exception? DeleteThrows { get; set; }

    public List<RecentLink> Deleted { get; } = [];

    public bool CanDelete(RecentLink link) => Deletes;

    public Task DeleteAsync(RecentLink link, CancellationToken cancellationToken = default)
    {
        if (DeleteThrows is not null)
        {
            return Task.FromException(DeleteThrows);
        }

        Deleted.Add(link);
        return Task.CompletedTask;
    }
}

internal sealed class FakeClipboard : IClipboard
{
    public List<string> Texts { get; } = [];

    public List<string> Images { get; } = [];

    public List<string> Order { get; } = [];

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Texts.Add(text);
        Order.Add("text");
        return Task.CompletedTask;
    }

    public string? Text { get; set; }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult(Text);

    public Task SetImageAsync(string pngFilePath, CancellationToken cancellationToken = default)
    {
        Images.Add(pngFilePath);
        Order.Add("image");
        return Task.CompletedTask;
    }
}

internal sealed class FakeUrlOpener : IUrlOpener
{
    public List<Uri> Opened { get; } = [];

    public Exception? Throws { get; set; }

    public void Open(Uri url)
    {
        if (Throws is not null)
        {
            throw Throws;
        }

        Opened.Add(url);
    }
}

internal sealed class FakeCaptureHost : ICaptureHost
{
    public List<string> Events { get; } = [];

    public bool Hidden { get; private set; }

    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        Hidden = true;
        Events.Add("hide");
        return Task.CompletedTask;
    }

    public Task ShowAsync(CancellationToken cancellationToken = default)
    {
        Hidden = false;
        Events.Add("show");
        return Task.CompletedTask;
    }
}

internal sealed class FakeCredentialStore : ICredentialStore
{
    public Dictionary<string, StoredCredential> Stored { get; } = [];

    public List<(string Name, StoredCredential Credential)> Writes { get; } = [];

    public Exception? WriteThrows { get; set; }

    public StoredCredential? Read(string name) => Stored.GetValueOrDefault(name);

    public void Write(string name, StoredCredential credential)
    {
        if (WriteThrows is not null)
        {
            throw WriteThrows;
        }

        Writes.Add((name, credential));
        Stored[name] = credential;
    }
}

internal sealed class FakeGlobalHotkeys : IGlobalHotkeys
{
    private EventHandler<BarAction>? _pressed;

    public Dictionary<BarAction, HotkeyChord> Registered { get; } = [];

    /// <summary>Chords another app holds; registering one reports it as not registered.</summary>
    public HashSet<HotkeyChord> Taken { get; } = [];

    public bool Disposed { get; private set; }

    public int RegisterCalls { get; private set; }

    public int PressedSubscribers => _pressed?.GetInvocationList().Length ?? 0;

    /// <summary>How many recordings are active right now.</summary>
    public int RecordingsHeld => _recorders.Count;

    public int RecordCalls { get; private set; }

    /// <summary>Runs at the start of each Register, so a test can see what state the caller is in then.</summary>
    public Action? OnRegister { get; set; }

    private readonly List<Action<HotkeyChord>> _recorders = [];

    /// <summary>What the keyboard hook does when a chord is pressed while recording: it goes to the newest recording.</summary>
    public void Deliver(HotkeyChord chord) => _recorders.LastOrDefault()?.Invoke(chord);

    public IDisposable Record(Action<HotkeyChord> onChord)
    {
        RecordCalls++;
        _recorders.Add(onChord);
        return new RecordHandle(this, onChord);
    }

    private sealed class RecordHandle(FakeGlobalHotkeys owner, Action<HotkeyChord> onChord) : IDisposable
    {
        public void Dispose() => owner._recorders.Remove(onChord);
    }

    public event EventHandler<BarAction>? Pressed
    {
        add => _pressed += value;
        remove => _pressed -= value;
    }

    public void Press(BarAction action) => _pressed?.Invoke(this, action);

    public IReadOnlyList<HotkeyRegistration> Register(IReadOnlyDictionary<BarAction, HotkeyChord> chords)
    {
        RegisterCalls++;
        OnRegister?.Invoke();
        Registered.Clear();
        List<HotkeyRegistration> results = [];
        foreach ((BarAction action, HotkeyChord chord) in chords)
        {
            Registered[action] = chord;
            results.Add(new HotkeyRegistration(action, chord, !Taken.Contains(chord)));
        }

        return results;
    }

    public void Dispose() => Disposed = true;
}
