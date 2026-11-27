using System.Text;
using System.Text.Json;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class NativeShareRequestHandlerTests : IDisposable
{
    private static readonly string Origin = $"chrome-extension://{ShareDefaults.ExtensionId}/";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-native-{Guid.NewGuid():N}");
    private readonly FakeUploader _uploader = new();

    public NativeShareRequestHandlerTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string RecentPath => Path.Combine(_folder, "recent.json");

    private NativeShareRequestHandler Handler(NativeHostSettings? settings = null) => new(
        settings ?? new NativeHostSettings(),
        new CaptureFiles(new CaptureSettings { Folder = Path.Combine(_folder, "captures") }, _folder, () => DateTimeOffset.UtcNow),
        () => _uploader,
        () => new ExpirySettings(),
        new RecentLinks(RecentPath, new RecentSettings()));

    private static byte[] Png() => PngEncoder.Encode(new ScreenFrame(0, 0, 3, 2, new byte[3 * 2 * ScreenFrame.BytesPerPixel]));

    private static byte[] Request(byte[] png, string mode = "fullPage", string type = "share") => Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { type, pngBase64 = Convert.ToBase64String(png), pageUrl = "https://page.example.test/a", mode }));

    [Fact]
    public async Task APngWithNoShareHostIsSavedAndItsPathReturned()
    {
        _uploader.IsConfigured = false;

        NativeShareReply reply = await Handler().HandleAsync(Origin, Request(Png()));

        Assert.True(reply.Ok);
        Assert.True(reply.Local);
        Assert.Null(reply.Link);
        Assert.StartsWith(Path.Combine(_folder, "captures"), reply.Path);
        Assert.Equal(Png(), await File.ReadAllBytesAsync(reply.Path!));
        Assert.Empty(_uploader.Uploads);
    }

    [Fact]
    public async Task AnUploadReturnsTheLinkAndTheRecentListGainsIt()
    {
        NativeShareReply reply = await Handler().HandleAsync(Origin, Request(Png(), "visible"));

        Assert.True(reply.Ok);
        Assert.False(reply.Local);
        Assert.Equal("https://share.example.test/abc", reply.Link);
        Assert.Null(reply.Path);
        Assert.Single(_uploader.Uploads);
        RecentLink recent = Assert.Single(new RecentLinks(RecentPath, new RecentSettings()).Load());
        Assert.Equal("https://share.example.test/abc", recent.Url);
        Assert.Equal("image/png", recent.ContentType);
    }

    [Fact]
    public async Task AFailedUploadGivesTheReasonAndKeepsTheFile()
    {
        _uploader.Throws = new ShareUploadException("The host said no.");

        NativeShareReply reply = await Handler().HandleAsync(Origin, Request(Png(), "element"));

        Assert.False(reply.Ok);
        Assert.Contains("The host said no.", reply.Error);
        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "captures")));
    }

    [Fact]
    public async Task AWrongOriginIsRefusedAndNothingIsSaved()
    {
        NativeShareReply reply = await Handler().HandleAsync("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/", Request(Png()));

        AssertRefused(reply);
        Assert.False(Directory.Exists(Path.Combine(_folder, "captures")));
    }

    [Fact]
    public async Task AnOriginAddedInTheSettingsIsServed()
    {
        _uploader.IsConfigured = false;
        NativeHostSettings settings = new() { AllowedOrigins = ["chrome-extension://bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/"] };

        Assert.True((await Handler(settings).HandleAsync("chrome-extension://bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/", Request(Png()))).Ok);
        AssertRefused(await Handler(settings).HandleAsync(Origin, Request(Png())));
    }

    [Fact]
    public async Task BadBase64IsRefused()
    {
        byte[] message = Encoding.UTF8.GetBytes("{\"type\":\"share\",\"pngBase64\":\"not base64 !!\",\"mode\":\"visible\"}");

        AssertRefused(await Handler().HandleAsync(Origin, message));
    }

    [Fact]
    public async Task BytesThatAreNotAPngAreRefused()
    {
        AssertRefused(await Handler().HandleAsync(Origin, Request("GIF89a-not-a-png"u8.ToArray())));
        Assert.False(Directory.Exists(Path.Combine(_folder, "captures")));
    }

    [Fact]
    public async Task AnUnknownModeIsRefused()
    {
        AssertRefused(await Handler().HandleAsync(Origin, Request(Png(), "everything")));
    }

    [Fact]
    public async Task AnUnknownTypeIsRefused()
    {
        AssertRefused(await Handler().HandleAsync(Origin, Request(Png(), type: "delete")));
    }

    [Fact]
    public async Task MessageThatIsNotJsonIsRefused()
    {
        AssertRefused(await Handler().HandleAsync(Origin, "not json"u8.ToArray()));
    }

    [Fact]
    public async Task TheReplyIsJsonWithOnlyTheFieldsOfItsShape()
    {
        NativeShareReply link = await Handler().HandleAsync(Origin, Request(Png()));
        NativeShareReply error = await Handler().HandleAsync("nope", Request(Png()));

        Assert.Equal("{\"ok\":true,\"link\":\"https://share.example.test/abc\",\"local\":false}", Encoding.UTF8.GetString(link.ToJson()));
        Assert.StartsWith("{\"ok\":false,\"error\":\"", Encoding.UTF8.GetString(error.ToJson()));
    }

    private static void AssertRefused(NativeShareReply reply)
    {
        Assert.False(reply.Ok);
        Assert.False(string.IsNullOrWhiteSpace(reply.Error));
        Assert.Null(reply.Link);
        Assert.Null(reply.Path);
    }
}
