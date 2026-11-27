using System.Text.Json;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// A web page picture from Edge or Chrome run headless with an empty profile, driven over the DevTools protocol. Each capture starts
/// its own browser and always ends it. Pages open signed out, because the profile holds no cookies.
/// </summary>
public sealed class CdpWebPageCapture(CaptureFiles files, Func<WebCaptureSettings> settings, string? profileRoot = null) : IWebPageCapture
{
    private const int PngWidthOffset = 16;

    private readonly string _profileRoot = profileRoot ?? Path.Combine(Path.GetTempPath(), "NoMercy Share", "web");

    /// <summary>The process id of the browser the last capture started; the tests look that it is gone.</summary>
    internal int? LastBrowserProcessId { get; private set; }

    public async Task<CaptureResult> CaptureAsync(WebCaptureRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page is not { } page || !WebAddress.IsWeb(page))
        {
            throw new WebCaptureException(WebCaptureField.Address, Strings.Get("web.error.address"));
        }

        WebCaptureSettings current = settings();
        string browser = BrowserLocator.Find(current.BrowserPath) ?? throw new WebCaptureException(WebCaptureField.None, Strings.Get("web.error.browser"));
        using BrowserProcess process = BrowserProcess.Start(browser, Path.Combine(_profileRoot, Guid.NewGuid().ToString("N")));
        TimeSpan limit = TimeSpan.FromSeconds(current.LoadTimeoutSeconds);
        using CdpConnection connection = await WithTimeout(limit, cancellationToken, async token =>
        {
            Uri pageSocket = await process.WaitForPageAsync(token);
            LastBrowserProcessId = process.Id;
            CdpConnection opened = await CdpConnection.ConnectAsync(pageSocket, token);
            try
            {
                await OpenPageAsync(opened, page, request.Device, current, token);
            }
            catch
            {
                opened.Dispose();
                throw;
            }

            return opened;
        });
        byte[] png = await WithTimeout(limit, cancellationToken, token => TakePictureAsync(connection, request, current, token));
        string path = files.NewPath();
        await File.WriteAllBytesAsync(path, png, cancellationToken);
        return new CaptureResult(path, "image/png", ReadInt(png, PngWidthOffset), ReadInt(png, PngWidthOffset + 4), png.Length);
    }

    /// <summary>Runs a step under a time limit; running out of time is "the page did not load in time", a cancel from the caller stays a cancel.</summary>
    private static async Task<T> WithTimeout<T>(TimeSpan limit, CancellationToken cancellationToken, Func<CancellationToken, Task<T>> step)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        try
        {
            return await step(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WebCaptureException(WebCaptureField.Address, Strings.Get("web.error.timeout"));
        }
    }

    private static async Task OpenPageAsync(CdpConnection connection, Uri page, WebCaptureDevice device, WebCaptureSettings current, CancellationToken token)
    {
        await connection.SendAsync("Page.enable", null, token);
        await connection.SendAsync("Emulation.setDeviceMetricsOverride", new
        {
            width = device == WebCaptureDevice.Phone ? current.PhoneWidth : current.DesktopWidth,
            height = current.Height,
            deviceScaleFactor = current.ScaleFactor,
            mobile = device == WebCaptureDevice.Phone
        }, token);
        Task loaded = connection.NextEvent("Page.loadEventFired");
        JsonElement navigation = await connection.SendAsync("Page.navigate", new { url = page.AbsoluteUri }, token);
        if (navigation.TryGetProperty("errorText", out JsonElement problem) && problem.GetString() is { Length: > 0 })
        {
            throw new WebCaptureException(WebCaptureField.Address, Strings.Get("web.error.unreachable"));
        }

        await loaded.WaitAsync(token);
        await Task.Delay(current.SettleMilliseconds, token);
    }

    private static async Task<byte[]> TakePictureAsync(CdpConnection connection, WebCaptureRequest request, WebCaptureSettings current, CancellationToken token)
    {
        WebClip? clip = request.Kind switch
        {
            WebCaptureKind.Viewport => null,
            WebCaptureKind.Element => await ElementClipAsync(connection, request.ElementSelector, current, token),
            _ => await FullPageClipAsync(connection, current, token)
        };
        object parameters = clip is { } area
            ? new { format = "png", captureBeyondViewport = true, clip = new { x = area.X, y = area.Y, width = area.Width, height = area.Height, scale = 1 } }
            : new { format = "png", captureBeyondViewport = false };
        JsonElement shot = await connection.SendAsync("Page.captureScreenshot", parameters, token);
        return Convert.FromBase64String(shot.GetProperty("data").GetString()!);
    }

    private static async Task<WebClip> FullPageClipAsync(CdpConnection connection, WebCaptureSettings current, CancellationToken token)
    {
        JsonElement size = (await connection.SendAsync("Page.getLayoutMetrics", null, token)).GetProperty("cssContentSize");
        return WebClip.FullPage(size.GetProperty("width").GetDouble(), size.GetProperty("height").GetDouble(), current.MaxFullPageHeight);
    }

    private static async Task<WebClip> ElementClipAsync(CdpConnection connection, string? selector, WebCaptureSettings current, CancellationToken token)
    {
        string script = $$"""
            (() => {
              const element = document.querySelector({{JsonSerializer.Serialize(selector)}});
              if (!element) { return null; }
              const box = element.getBoundingClientRect();
              return { left: box.left, top: box.top, width: box.width, height: box.height, scrollX: window.scrollX, scrollY: window.scrollY };
            })()
            """;
        JsonElement evaluated = await connection.SendAsync("Runtime.evaluate", new { expression = script, returnByValue = true }, token);
        if (evaluated.TryGetProperty("exceptionDetails", out _) || !evaluated.GetProperty("result").TryGetProperty("value", out JsonElement box) || box.ValueKind != JsonValueKind.Object)
        {
            throw new WebCaptureException(WebCaptureField.Selector, Strings.Get("web.error.nomatch"));
        }

        WebClip clip = WebClip.Element(
            box.GetProperty("left").GetDouble(), box.GetProperty("top").GetDouble(), box.GetProperty("width").GetDouble(), box.GetProperty("height").GetDouble(),
            box.GetProperty("scrollX").GetDouble(), box.GetProperty("scrollY").GetDouble(), current.MaxFullPageHeight);
        return clip.IsEmpty ? throw new WebCaptureException(WebCaptureField.Selector, Strings.Get("web.error.empty")) : clip;
    }

    private static int ReadInt(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
}
