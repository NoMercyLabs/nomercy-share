using System.Text.Json;
using NoMercy.Share.Core.Destinations;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class DiscordWebhookDestinationTests : IDisposable
{
    private const string Webhook = "https://discord.com/api/webhooks/000/made-up-token";

    private static readonly ShareLink Link = new(new Uri("https://share.example.test/exp-7d/abc.png"), null);

    private readonly FakeDiscordServer _server = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly List<TimeSpan> _waits = [];
    private readonly HttpClient _http;
    private DiscordSettings _settings = new() { Enabled = true };

    public DiscordWebhookDestinationTests()
    {
        _http = new HttpClient(new RedirectToHandler(_server.Address));
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", Webhook);
    }

    private DiscordWebhookDestination Destination() => new(
        _http, () => _settings, _credentials, (wait, _) =>
        {
            _waits.Add(wait);
            return Task.CompletedTask;
        });

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }

    [Fact]
    public void TheDefaultsAreOffAndNamed()
    {
        DiscordSettings defaults = new();

        Assert.False(defaults.Enabled);
        Assert.Equal("nomercy-share-discord", defaults.CredentialName);
        Assert.Equal(10, defaults.TimeoutSeconds);
        Assert.False(new ShareSettings().Discord.Enabled);
    }

    [Fact]
    public async Task ItPostsExactlyTheLinkAsJson()
    {
        await Destination().SendAsync(Link, "image/png");

        (string method, string path, string contentType, string body) = Assert.Single(_server.Requests);
        Assert.Equal("POST", method);
        Assert.Equal("/api/webhooks/000/made-up-token", path);
        Assert.StartsWith("application/json", contentType, StringComparison.Ordinal);
        Assert.Equal("{\"content\":\"https://share.example.test/exp-7d/abc.png\"}", body);
    }

    [Fact]
    public void ItIsConfiguredOnlyWhenOnAndAWebhookIsStored()
    {
        Assert.True(Destination().IsConfigured);

        _settings = new DiscordSettings { Enabled = false };
        Assert.False(Destination().IsConfigured);

        _settings = new DiscordSettings { Enabled = true, CredentialName = "no-such-name" };
        Assert.False(Destination().IsConfigured);
    }

    [Fact]
    public async Task TheWebhookIsReadFromTheStoreUnderTheSettingsName()
    {
        _credentials.Stored["my-discord"] = new StoredCredential("webhook", "https://discord.com/api/webhooks/111/other-token");
        _settings = new DiscordSettings { Enabled = true, CredentialName = "my-discord" };

        await Destination().SendAsync(Link, "image/png");

        Assert.Equal("/api/webhooks/111/other-token", Assert.Single(_server.Requests).Path);
    }

    [Fact]
    public void TheSettingsFileNeverHoldsTheWebhook()
    {
        string json = JsonSerializer.Serialize(new ShareSettings { Discord = _settings });

        Assert.DoesNotContain("made-up-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("discord.com", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWebhookThatIsNotDiscordsIsRefusedBeforeAnythingIsSent()
    {
        _credentials.Stored[ShareDefaults.DiscordCredentialName] = new StoredCredential("webhook", "https://example.test/api/webhooks/000/made-up-token");

        LinkDestinationException error = await Assert.ThrowsAsync<LinkDestinationException>(() => Destination().SendAsync(Link, "image/png"));

        Assert.Empty(_server.Requests);
        Assert.DoesNotContain("made-up-token", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARateLimitIsWaitedOutOnceAndThenTheSecondTrySucceeds()
    {
        _server.Answer(429, "{\"retry_after\": 1.5}");
        _server.Answer(204);

        await Destination().SendAsync(Link, "image/png");

        Assert.Equal(2, _server.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1.5)], _waits);
    }

    [Fact]
    public async Task ASecondRateLimitFails()
    {
        _server.Answer(429, "{\"retry_after\": 1}");

        LinkDestinationException error = await Assert.ThrowsAsync<LinkDestinationException>(() => Destination().SendAsync(Link, "image/png"));

        Assert.Equal(2, _server.Requests.Count);
        Assert.Contains("429", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARateLimitLongerThanTheTimeoutFailsWithoutWaiting()
    {
        _server.Answer(429, "{\"retry_after\": 60}");

        await Assert.ThrowsAsync<LinkDestinationException>(() => Destination().SendAsync(Link, "image/png"));

        Assert.Single(_server.Requests);
        Assert.Empty(_waits);
    }

    [Fact]
    public async Task AnErrorStatusFailsAndNamesTheStatus()
    {
        _server.Answer(404, "{\"message\": \"Unknown Webhook\"}");

        LinkDestinationException error = await Assert.ThrowsAsync<LinkDestinationException>(() => Destination().SendAsync(Link, "image/png"));

        Assert.Contains("404", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTimeoutSettingEndsASlowAnswer()
    {
        _server.Answer(204, delay: TimeSpan.FromSeconds(5));
        _settings = new DiscordSettings { Enabled = true, TimeoutSeconds = 1 };

        LinkDestinationException error = await Assert.ThrowsAsync<LinkDestinationException>(() => Destination().SendAsync(Link, "image/png"));

        Assert.Contains("timed out", error.Message, StringComparison.Ordinal);
    }
}
