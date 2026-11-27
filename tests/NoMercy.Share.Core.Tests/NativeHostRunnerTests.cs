using System.Text;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Tests;

public sealed class NativeHostRunnerTests : IDisposable
{
    private static readonly string Origin = $"chrome-extension://{ShareDefaults.ExtensionId}/";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-runner-{Guid.NewGuid():N}");

    public NativeHostRunnerTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private NativeHostRunner Runner(NativeHostSettings settings) => new(
        new NativeMessageCodec(settings),
        new NativeShareRequestHandler(
            settings,
            new CaptureFiles(new CaptureSettings { Folder = _folder }, _folder, () => DateTimeOffset.UtcNow),
            () => new FakeUploader { IsConfigured = false },
            () => new ExpirySettings(),
            new RecentLinks(Path.Combine(_folder, "recent.json"), new RecentSettings())));

    private static async Task<MemoryStream> Frame(byte[] payload)
    {
        MemoryStream stream = new();
        await new NativeMessageCodec(new NativeHostSettings()).WriteAsync(stream, payload);
        stream.Position = 0;
        return stream;
    }

    private static async Task<JsonElement> ReadReply(MemoryStream output)
    {
        output.Position = 0;
        byte[]? reply = await new NativeMessageCodec(new NativeHostSettings()).ReadAsync(output);
        return JsonDocument.Parse(reply ?? []).RootElement;
    }

    [Fact]
    public async Task OneRequestGetsOneFramedReply()
    {
        byte[] png = PngEncoder.Encode(new ScreenFrame(0, 0, 1, 1, new byte[4]));
        byte[] request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "share", pngBase64 = Convert.ToBase64String(png), mode = "visible" }));
        MemoryStream output = new();

        await Runner(new NativeHostSettings()).RunAsync(Origin, await Frame(request), output);

        JsonElement reply = await ReadReply(output);
        Assert.True(reply.GetProperty("ok").GetBoolean());
        Assert.True(reply.GetProperty("local").GetBoolean());
        Assert.True(File.Exists(reply.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task ATooBigFrameGetsAnErrorReply()
    {
        MemoryStream input = new(BitConverter.GetBytes(3 * 1024 * 1024));
        MemoryStream output = new();

        await Runner(new NativeHostSettings { MaxMessageMegabytes = 1 }).RunAsync(Origin, input, output);

        JsonElement reply = await ReadReply(output);
        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(reply.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task ATruncatedFrameAndAnEmptyInputGetAnErrorReply()
    {
        foreach (byte[] bytes in new[] { [.. BitConverter.GetBytes(10), 1], Array.Empty<byte>() })
        {
            MemoryStream output = new();

            await Runner(new NativeHostSettings()).RunAsync(Origin, new MemoryStream(bytes), output);

            Assert.False((await ReadReply(output)).GetProperty("ok").GetBoolean());
        }
    }

    [Fact]
    public void OnlyAnExtensionOriginMarksHostMode()
    {
        Assert.Equal(Origin, NativeHostArguments.Origin([Origin, "--parent-window=1234"]));
        Assert.Null(NativeHostArguments.Origin([]));
        Assert.Null(NativeHostArguments.Origin(["--parent-window=1234"]));
        Assert.Null(NativeHostArguments.Origin(["https://example.test/"]));
    }
}
