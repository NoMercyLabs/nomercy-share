using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

internal sealed class FakeScreenCapture : IScreenCapture
{
    public List<string> Calls { get; } = [];

    public Exception? Throws { get; set; }

    /// <summary>Runs inside every capture, so a test can look at the state at that moment.</summary>
    public Action? OnCapture { get; set; }

    public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default) => Run("region");

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

    public event EventHandler<string>? Failed;

    /// <summary>Ends the recording the way a dying ffmpeg does: no stop call, only the event.</summary>
    public void Fail(string message)
    {
        IsRecording = false;
        Failed?.Invoke(this, message);
    }

    public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        if (StartThrows is not null)
        {
            return Task.FromException(StartThrows);
        }

        IsRecording = true;
        return Task.CompletedTask;
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

    public Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(new CaptureResult("/tmp/page.png", "image/png"));
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

    public Task SetImageAsync(string pngFilePath, CancellationToken cancellationToken = default)
    {
        Images.Add(pngFilePath);
        Order.Add("image");
        return Task.CompletedTask;
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
