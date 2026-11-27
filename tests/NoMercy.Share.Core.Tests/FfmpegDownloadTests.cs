using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

/// <summary>The NoMercy ffmpeg download: the latest release, the asset for this PC, both checksums, and only ffmpeg unpacked.</summary>
public sealed class FfmpegDownloadTests : IDisposable
{
    private const string Release = "https://api.example.test/releases/latest";
    private const string Asset = "ffmpeg-9.1-windows-x86_64-v1.0.50.zip";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-ffmpeg-download-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static byte[] Zip()
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string body) in new[] { ("ffmpeg-9.1/bin/ffmpeg.exe", "the encoder"), ("ffmpeg-9.1/bin/ffplay.exe", "a player"), ("ffmpeg-9.1/README.txt", "docs") })
            {
                using StreamWriter writer = new(zip.CreateEntry(name).Open());
                writer.Write(body);
            }
        }

        return stream.ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private FfmpegDownload Download(byte[] zip, string? digest = null, string? manifestSha = null)
    {
        string release = JsonSerializer.Serialize(new
        {
            tag_name = "v1.0.50",
            assets = new object[]
            {
                new { name = "ffmpeg-9.1-windows-aarch64-v1.0.50.zip", browser_download_url = "https://dl.example.test/arm.zip", digest = "sha256:00", size = 1 },
                new { name = Asset, browser_download_url = "https://dl.example.test/x64.zip", digest = digest ?? $"sha256:{Sha256(zip)}", size = zip.Length },
                new { name = "manifest.json", browser_download_url = "https://dl.example.test/manifest.json", digest = "", size = 1 }
            }
        });
        string manifest = JsonSerializer.Serialize(new { assets = new[] { new { name = Asset, sha256 = manifestSha ?? Sha256(zip) } } });
        HttpClient http = new(new Routes(new Dictionary<string, byte[]>
        {
            [Release] = Encoding.UTF8.GetBytes(release),
            ["https://dl.example.test/manifest.json"] = Encoding.UTF8.GetBytes(manifest),
            ["https://dl.example.test/x64.zip"] = zip
        }));
        return new FfmpegDownload(http, _folder, Release, "windows-x86_64", () => File.Exists(Path.Combine(_folder, FfmpegLocator.ExecutableName)));
    }

    [Fact]
    public async Task ItUnpacksOnlyFfmpegFromTheAssetForThisPc()
    {
        FfmpegDownload download = Download(Zip());
        List<double> progress = [];

        await download.DownloadAsync(new Inline(progress.Add));

        Assert.Equal("the encoder", await File.ReadAllTextAsync(Path.Combine(_folder, FfmpegLocator.ExecutableName)));
        Assert.Equal([FfmpegLocator.ExecutableName], Directory.GetFiles(_folder).Select(Path.GetFileName));
        Assert.True(download.IsFound());
        Assert.Equal(1, progress[^1]);
    }

    [Fact]
    public async Task AFileThatDoesNotMatchGithubsChecksumIsRefusedAndNothingIsInstalled()
    {
        FfmpegDownload download = Download(Zip(), digest: "sha256:" + new string('0', 64));

        await Assert.ThrowsAsync<IOException>(() => download.DownloadAsync(new Inline(_ => { })));

        Assert.False(Directory.Exists(_folder) && Directory.EnumerateFileSystemEntries(_folder).Any());
    }

    [Fact]
    public async Task AFileThatDoesNotMatchTheReleaseManifestIsRefused()
    {
        FfmpegDownload download = Download(Zip(), manifestSha: new string('1', 64));

        await Assert.ThrowsAsync<IOException>(() => download.DownloadAsync(new Inline(_ => { })));

        Assert.False(download.IsFound());
    }

    private sealed class Inline(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class Routes(Dictionary<string, byte[]> bodies) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(bodies.TryGetValue(request.RequestUri!.ToString(), out byte[]? body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
