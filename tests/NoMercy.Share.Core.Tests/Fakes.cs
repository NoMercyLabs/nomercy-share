using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

internal sealed class FakeScreenCapture : IScreenCapture
{
    public List<string> Calls { get; } = [];

    public Exception? Throws { get; set; }

    public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default) => Run("region");

    public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) => Run("window");

    public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) => Run("fullscreen");

    private Task<CaptureResult> Run(string call)
    {
        Calls.Add(call);
        return Throws is null ? Task.FromResult(new CaptureResult($"/tmp/{call}.png", "image/png")) : Task.FromException<CaptureResult>(Throws);
    }
}

internal sealed class FakeScreenRecorder : IScreenRecorder
{
    public bool IsRecording { get; private set; }

    public Exception? StartThrows { get; set; }

    public RecordingOptions? StartedWith { get; private set; }

    public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        if (StartThrows is not null)
        {
            return Task.FromException(StartThrows);
        }

        StartedWith = options;
        IsRecording = true;
        return Task.CompletedTask;
    }

    public Task<CaptureResult> StopAsync(CancellationToken cancellationToken = default)
    {
        IsRecording = false;
        return Task.FromResult(new CaptureResult("/tmp/recording.mp4", "video/mp4"));
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

    public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default)
    {
        Uploads.Add((filePath, expiry));
        return Task.FromResult(new ShareLink(new Uri("https://share.example.test/abc"), null));
    }
}

internal sealed class FakeClipboard : IClipboard
{
    public List<string> Texts { get; } = [];

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}
