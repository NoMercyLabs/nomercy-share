using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.Discord.Tests;

public sealed class DiscordShareUploaderTests : IDisposable
{
    private const string Webhook = "https://discord.com/api/webhooks/000/made-up-token";
    private const string Attachment = "https://cdn.example.test/attachments/1/2/shot.png";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"share-discord-uploader-test-{Guid.NewGuid():N}");
    private readonly FakeDiscordServer _server = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly List<TimeSpan> _waits = [];
    private readonly HttpClient _http;
    private readonly string _file;
    private ShareSettings _settings = new() { Storage = new StorageSettings { Provider = "discord" } };

    public DiscordShareUploaderTests()
    {
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, "nomercy-share-20261127T101500000Z.png");
        File.WriteAllText(_file, "made-up-picture-bytes");
        _http = new HttpClient(new RedirectToHandler(_server.Address));
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Webhook);
    }

    private DiscordShareUploader Uploader() => new(
        _http, _settings, _credentials, (wait, _) =>
        {
            _waits.Add(wait);
            return Task.CompletedTask;
        });

    private static string Message(string url) => $$"""{"id":"1","attachments":[{"id":"2","filename":"shot.png","url":"{{url}}"}]}""";

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void ItAnswersToTheNameDiscord()
    {
        Assert.Equal("discord", Uploader().ProviderName);
    }

    [Fact]
    public async Task ItPostsTheFileAsMultipartWithPayloadJsonAndWaitOnTheUrl()
    {
        _server.Answer(200, Message(Attachment));

        await Uploader().UploadAsync(_file, Expiry.InDays(7));

        (string method, string path, string query, string contentType, string body) = Assert.Single(_server.Requests);
        Assert.Equal("POST", method);
        Assert.Equal("/api/webhooks/000/made-up-token", path);
        Assert.Equal("?wait=true", query);
        Assert.StartsWith("multipart/form-data", contentType, StringComparison.Ordinal);
        Assert.Contains("files[0]", body, StringComparison.Ordinal);
        Assert.Contains("made-up-picture-bytes", body, StringComparison.Ordinal);
        Assert.Contains("payload_json", body, StringComparison.Ordinal);
        Assert.Contains("\"attachments\":[{\"id\":0,\"filename\":\"", body, StringComparison.Ordinal);
        Assert.Contains(".png\"}]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("20261127T101500000Z", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReturnedAttachmentAddressBecomesTheLink()
    {
        _server.Answer(200, Message(Attachment));

        ShareLink link = await Uploader().UploadAsync(_file, Expiry.Never);

        Assert.Equal(Attachment, link.Url.AbsoluteUri);
    }

    [Fact]
    public async Task TheExQueryHoldsTheExpiryAsHexUnixSeconds()
    {
        DateTimeOffset expires = new(2026, 12, 4, 10, 0, 0, TimeSpan.Zero);
        string hex = expires.ToUnixTimeSeconds().ToString("x", System.Globalization.CultureInfo.InvariantCulture);
        _server.Answer(200, Message($"{Attachment}?ex={hex}&is=abc&hm=def"));

        ShareLink link = await Uploader().UploadAsync(_file, Expiry.Never);

        Assert.Equal(expires, link.ExpiresAt);
    }

    [Fact]
    public async Task WithNoExTheExpiryIsUnknownNotNever()
    {
        _server.Answer(200, Message($"{Attachment}?is=abc&hm=def"));

        ShareLink link = await Uploader().UploadAsync(_file, Expiry.InDays(7));

        Assert.Null(link.ExpiresAt);
    }

    [Fact]
    public async Task AnExThatIsNotHexIsUnknown()
    {
        _server.Answer(200, Message($"{Attachment}?ex=not-hex"));

        ShareLink link = await Uploader().UploadAsync(_file, Expiry.Never);

        Assert.Null(link.ExpiresAt);
    }

    [Fact]
    public async Task AFileOverTheCapFailsBeforeAnyRequest()
    {
        _settings = _settings with { Discord = new DiscordSettings { MaxUploadMegabytes = 0.00001 } };

        await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task TheSizeMessageNamesBothSizesInMegabytes()
    {
        string big = Path.Combine(_folder, "big.mp4");
        await File.WriteAllBytesAsync(big, new byte[3 * 1024 * 1024 + 512 * 1024]);
        _settings = _settings with { Discord = new DiscordSettings { MaxUploadMegabytes = 2 } };

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(big, Expiry.Never));

        Assert.Equal("The file is 3.5 MB; Discord takes up to 2 MB", error.Message);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task AnAnswerOf413GivesTheSameSizeMessage()
    {
        _server.Answer(413, "{}");

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.StartsWith("The file is ", error.Message, StringComparison.Ordinal);
        Assert.Contains("Discord takes up to 10 MB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARateLimitIsWaitedOutOnceThenTheUploadGoesThrough()
    {
        _server.Answer(429, "{\"retry_after\":0.5}");
        _server.Answer(200, Message(Attachment));

        ShareLink link = await Uploader().UploadAsync(_file, Expiry.Never);

        Assert.Equal(Attachment, link.Url.AbsoluteUri);
        Assert.Equal(2, _server.Requests.Count);
        Assert.Equal(TimeSpan.FromSeconds(0.5), Assert.Single(_waits));
    }

    [Fact]
    public async Task ASecondRateLimitFailsWithTheStatus()
    {
        _server.Answer(429, "{\"retry_after\":0}");

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal("Discord refused it (429)", error.Message);
        Assert.Equal(2, _server.Requests.Count);
    }

    [Fact]
    public async Task ARateLimitLongerThanTheTimeoutFailsWithoutWaiting()
    {
        _settings = _settings with { Discord = new DiscordSettings { TimeoutSeconds = 2 } };
        _server.Answer(429, "{\"retry_after\":30}");

        await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Empty(_waits);
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task AnAnswerWithoutAnAttachmentAddressFails()
    {
        _server.Answer(200, "{\"id\":\"1\",\"attachments\":[]}");

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal("Discord sent no link", error.Message);
    }

    [Fact]
    public async Task AnAnswerThatIsNotJsonFails()
    {
        _server.Answer(200, "<html>");

        await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));
    }

    [Fact]
    public async Task ATimeoutFailsWithItsOwnMessage()
    {
        _settings = _settings with { Discord = new DiscordSettings { TimeoutSeconds = 1 } };
        _server.Answer(200, Message(Attachment), TimeSpan.FromSeconds(3));

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal("Discord did not answer in time", error.Message);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task NoErrorMessageHoldsTheWebhookAddress(int status)
    {
        _server.Answer(status, "{\"message\":\"nope\"}");

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal($"Discord refused it ({status})", error.Message);
        Assert.DoesNotContain("made-up-token", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableDiscordFailsWithoutTheWebhookAddress()
    {
        _server.Dispose();

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal("Discord could not be reached", error.Message);
        Assert.DoesNotContain("made-up-token", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStoredAddressThatIsNotDiscordsFailsBeforeAnyRequest()
    {
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", "https://example.test/api/webhooks/000/made-up-token");

        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(_file, Expiry.Never));

        Assert.Equal("the Discord webhook address is not valid", error.Message);
        Assert.DoesNotContain("made-up-token", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task AMissingFileFails()
    {
        await Assert.ThrowsAsync<ShareUploadException>(() => Uploader().UploadAsync(Path.Combine(_folder, "gone.png"), Expiry.Never));
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task TheWebhookIsReadFromTheStoreUnderTheSettingsName()
    {
        _credentials.Stored["my-discord"] = new StoredCredential("webhook", "https://discord.com/api/webhooks/111/other-token");
        _settings = _settings with { Discord = new DiscordSettings { CredentialName = "my-discord" } };
        _server.Answer(200, Message(Attachment));

        await Uploader().UploadAsync(_file, Expiry.Never);

        Assert.Equal("/api/webhooks/111/other-token", Assert.Single(_server.Requests).Path);
    }

    [Fact]
    public void ItIsConfiguredOnlyWhenTheProviderIsDiscordAndAWebhookIsStored()
    {
        Assert.True(Uploader().IsConfigured);

        _settings = new ShareSettings { Storage = new StorageSettings { Provider = "r2" } };
        Assert.False(Uploader().IsConfigured);

        _settings = new ShareSettings { Storage = new StorageSettings { Provider = "discord" }, Discord = new DiscordSettings { CredentialName = "no-such-name" } };
        Assert.False(Uploader().IsConfigured);
    }
}
