using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

/// <summary>A fact that skips where there is no Edge or Chrome in their standard folders.</summary>
public sealed class WebBrowserFactAttribute : FactAttribute
{
    public WebBrowserFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || BrowserLocator.Find(string.Empty) is null)
        {
            Skip = "No Edge or Chrome was found on this machine.";
        }
    }
}

/// <summary>Captures a made-up page served from 127.0.0.1 with a real headless browser: 1280 wide, 3000 tall, a red #card of 300x200 at y=1500.</summary>
public sealed class CdpWebPageCaptureTests : IDisposable
{
    private const string Page = """
        <!doctype html><html><head><style>
        html,body{margin:0;padding:0}
        body{height:3000px;position:relative;background:#ffffff}
        #card{position:absolute;left:100px;top:1500px;width:300px;height:200px;background:rgb(255,0,0)}
        </style></head><body><div id="card"></div></body></html>
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"share-web-test-{Guid.NewGuid():N}");
    private readonly HttpListener _server = new();
    private readonly Uri _address;
    private readonly CdpWebPageCapture _capture;

    public CdpWebPageCaptureTests()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _address = new Uri($"http://127.0.0.1:{port}/");
        _server.Prefixes.Add(_address.ToString());
        _server.Start();
        _ = ServeAsync();
        _capture = new CdpWebPageCapture(
            new CaptureFiles(new CaptureSettings { Folder = Path.Combine(_root, "captures") }, _root, () => DateTimeOffset.UtcNow),
            () => new WebCaptureSettings { LoadTimeoutSeconds = 20 },
            Path.Combine(_root, "profiles"));
    }

    public void Dispose()
    {
        _server.Close();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [WebBrowserFact]
    public async Task TheVisibleAreaIsTheViewportAtTwiceTheScale()
    {
        CaptureResult result = await _capture.CaptureAsync(new WebCaptureRequest(_address, WebCaptureKind.Viewport));

        Assert.Equal((2560, 1600), Size(result));
        Assert.Equal((2560, 1600), (result.Width, result.Height));
        AssertBrowserGone();
    }

    [WebBrowserFact]
    public async Task TheFullPageIsAsTallAsTheContent()
    {
        CaptureResult result = await _capture.CaptureAsync(new WebCaptureRequest(_address, WebCaptureKind.FullPage));

        Assert.Equal((2560, 6000), Size(result));
        AssertBrowserGone();
    }

    [WebBrowserFact]
    public async Task OneElementIsJustThatElement()
    {
        CaptureResult result = await _capture.CaptureAsync(new WebCaptureRequest(_address, WebCaptureKind.Element, "#card"));

        PngPicture picture = PngPicture.Read(File.ReadAllBytes(result.FilePath));
        Assert.Equal((600, 400), (picture.Width, picture.Height));
        Assert.Equal((255, 0, 0), picture.Pixel(300, 200));
        AssertBrowserGone();
    }

    [WebBrowserFact]
    public async Task APhoneCaptureUsesThePhoneWidth()
    {
        CaptureResult result = await _capture.CaptureAsync(new WebCaptureRequest(_address, WebCaptureKind.Viewport, Device: WebCaptureDevice.Phone));

        Assert.Equal((780, 1600), Size(result));
        AssertBrowserGone();
    }

    [WebBrowserFact]
    public async Task ASelectorThatMatchesNothingIsAFieldError()
    {
        WebCaptureException error = await Assert.ThrowsAsync<WebCaptureException>(
            () => _capture.CaptureAsync(new WebCaptureRequest(_address, WebCaptureKind.Element, "#nothing-here")));

        Assert.Equal(WebCaptureField.Selector, error.Field);
        Assert.Equal("No element matches that selector.", error.Message);
        AssertBrowserGone();
    }

    [WebBrowserFact]
    public async Task APageThatNeverLoadsTimesOutAndTheBrowserIsStopped()
    {
        using TcpListener silent = new(IPAddress.Loopback, 0);
        silent.Start();
        Uri hung = new($"http://127.0.0.1:{((IPEndPoint)silent.LocalEndpoint).Port}/");
        CdpWebPageCapture quick = new(
            new CaptureFiles(new CaptureSettings { Folder = Path.Combine(_root, "captures") }, _root, () => DateTimeOffset.UtcNow),
            () => new WebCaptureSettings { LoadTimeoutSeconds = 2 },
            Path.Combine(_root, "profiles"));

        WebCaptureException error = await Assert.ThrowsAsync<WebCaptureException>(() => quick.CaptureAsync(new WebCaptureRequest(hung)));

        Assert.Equal(WebCaptureField.Address, error.Field);
        Assert.Equal("The page did not load in time.", error.Message);
        AssertBrowserGone(quick);
    }

    [Theory]
    [InlineData("file:///C:/made-up/page.html")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<p>hi</p>")]
    [InlineData("chrome://settings")]
    public async Task AnAddressThatIsNotHttpIsRefusedBeforeAnyBrowserStarts(string address)
    {
        CdpWebPageCapture refusing = new(
            new CaptureFiles(new CaptureSettings { Folder = Path.Combine(_root, "captures") }, _root, () => DateTimeOffset.UtcNow),
            () => new WebCaptureSettings { BrowserPath = Path.Combine(_root, "no-browser-here.exe") },
            Path.Combine(_root, "profiles"));

        WebCaptureException error = await Assert.ThrowsAsync<WebCaptureException>(() => refusing.CaptureAsync(new WebCaptureRequest(new Uri(address))));

        Assert.Equal(WebCaptureField.Address, error.Field);
        Assert.Equal("Enter a full https:// address.", error.Message);
        Assert.Null(refusing.LastBrowserProcessId);
        Assert.False(Directory.Exists(Path.Combine(_root, "profiles")));
    }

    [Fact]
    public async Task ABrowserPathThatDoesNotExistIsAnErrorOnNoField()
    {
        CdpWebPageCapture missing = new(
            new CaptureFiles(new CaptureSettings { Folder = Path.Combine(_root, "captures") }, _root, () => DateTimeOffset.UtcNow),
            () => new WebCaptureSettings { BrowserPath = Path.Combine(_root, "no-browser-here.exe") },
            Path.Combine(_root, "profiles"));

        WebCaptureException error = await Assert.ThrowsAsync<WebCaptureException>(() => missing.CaptureAsync(new WebCaptureRequest(_address)));

        Assert.Equal(WebCaptureField.None, error.Field);
        Assert.Null(missing.LastBrowserProcessId);
    }

    [Fact]
    public async Task ARequestWithoutAnAddressAsksForOne()
    {
        WebCaptureException error = await Assert.ThrowsAsync<WebCaptureException>(() => _capture.CaptureAsync(new WebCaptureRequest()));

        Assert.Equal(WebCaptureField.Address, error.Field);
    }

    private static (int Width, int Height) Size(CaptureResult result)
    {
        PngPicture picture = PngPicture.Read(File.ReadAllBytes(result.FilePath));
        return (picture.Width, picture.Height);
    }

    private void AssertBrowserGone(CdpWebPageCapture? capture = null)
    {
        int id = (capture ?? _capture).LastBrowserProcessId ?? throw new Xunit.Sdk.XunitException("no browser was started");
        Assert.False(IsRunning(id), $"browser process {id} is still running");
        string profiles = Path.Combine(_root, "profiles");
        Assert.True(!Directory.Exists(profiles) || Directory.GetDirectories(profiles).Length == 0, "a profile folder was left behind");
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using Process process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task ServeAsync()
    {
        while (_server.IsListening)
        {
            try
            {
                HttpListenerContext context = await _server.GetContextAsync();
                byte[] body = Encoding.UTF8.GetBytes(Page);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
        }
    }
}

/// <summary>Reads the header and the pixels of an 8-bit RGB or RGBA PNG that is not interlaced, which is what a browser writes.</summary>
internal sealed class PngPicture
{
    private readonly byte[] _pixels;
    private readonly int _bytesPerPixel;

    private PngPicture(int width, int height, int bytesPerPixel, byte[] pixels)
    {
        Width = width;
        Height = height;
        _bytesPerPixel = bytesPerPixel;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public (int R, int G, int B) Pixel(int x, int y)
    {
        int at = (y * Width + x) * _bytesPerPixel;
        return (_pixels[at], _pixels[at + 1], _pixels[at + 2]);
    }

    public static PngPicture Read(byte[] png)
    {
        int width = ReadInt(png, 16);
        int height = ReadInt(png, 20);
        int bytesPerPixel = png[25] == 6 ? 4 : 3;
        using MemoryStream compressed = new();
        for (int at = 8; at < png.Length;)
        {
            int length = ReadInt(png, at);
            if (Encoding.ASCII.GetString(png, at + 4, 4) == "IDAT")
            {
                compressed.Write(png, at + 8, length);
            }

            at += 12 + length;
        }

        compressed.Position = 0;
        using MemoryStream raw = new();
        using (ZLibStream inflate = new(compressed, CompressionMode.Decompress))
        {
            inflate.CopyTo(raw);
        }

        return new PngPicture(width, height, bytesPerPixel, Unfilter(raw.ToArray(), width, height, bytesPerPixel));
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int bytesPerPixel)
    {
        int stride = width * bytesPerPixel;
        byte[] pixels = new byte[stride * height];
        for (int row = 0; row < height; row++)
        {
            byte filter = raw[row * (stride + 1)];
            for (int column = 0; column < stride; column++)
            {
                int left = column >= bytesPerPixel ? pixels[(row * stride) + column - bytesPerPixel] : 0;
                int up = row > 0 ? pixels[((row - 1) * stride) + column] : 0;
                int upLeft = row > 0 && column >= bytesPerPixel ? pixels[((row - 1) * stride) + column - bytesPerPixel] : 0;
                int value = raw[(row * (stride + 1)) + 1 + column];
                pixels[(row * stride) + column] = (byte)(value + filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0
                });
            }
        }

        return pixels;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceUp = Math.Abs(estimate - up);
        int distanceUpLeft = Math.Abs(estimate - upLeft);
        return distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft ? left : distanceUp <= distanceUpLeft ? up : upLeft;
    }

    private static int ReadInt(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
}
