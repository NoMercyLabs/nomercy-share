using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Discord;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.Discord;

/// <summary>Uploads the file itself to a Discord channel through its webhook; the link is the file's Discord address. The webhook address is read from the credential store for each upload or delete and is never logged or put in a message.</summary>
public sealed class DiscordShareUploader(
    HttpClient http, ShareSettings settings, ICredentialStore credentials, Func<TimeSpan, CancellationToken, Task>? wait = null) : IShareUploader
{
    private const int RateLimited = 429;

    private const int TooLarge = 413;

    private const double BytesPerMegabyte = 1024 * 1024;

    private const int NameLength = 10;

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(1);

    private readonly Func<TimeSpan, CancellationToken, Task> _wait = wait ?? Task.Delay;

    public string ProviderName => DiscordSettings.ProviderName;

    public bool IsConfigured =>
        string.Equals(settings.Storage.Provider, DiscordSettings.ProviderName, StringComparison.OrdinalIgnoreCase)
        && credentials.Read(settings.Discord.CredentialName) is not null;

    /// <summary>Discord's link expiry is its own, so the requested one does not apply. Posts once; after a rate limit it waits the time Discord names (at most the timeout) and posts once more.</summary>
    public async Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default)
    {
        DiscordSettings discord = settings.Discord;
        if (!File.Exists(filePath))
        {
            throw new ShareUploadException(Strings.Get("error.upload.file"));
        }

        long size = new FileInfo(filePath).Length;
        if (size > discord.MaxUploadMegabytes * BytesPerMegabyte)
        {
            throw TooBig(size);
        }

        Uri address = new($"{ReadWebhook().AbsoluteUri}?wait=true");
        string name = RandomName(filePath);
        using HttpResponseMessage response = await SendWithRetryAsync(
            () => PostAsync(address, filePath, name, cancellationToken), cancellationToken);
        if ((int)response.StatusCode == TooLarge)
        {
            throw TooBig(size);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw Refused(response.StatusCode);
        }

        return await ReadLinkAsync(response, cancellationToken);
    }

    public bool CanDelete(RecentLink link) => link.HostId is not null;

    /// <summary>Deletes the webhook message that holds the file. Any refusal throws, because a 404 can also mean the webhook changed.</summary>
    public async Task DeleteAsync(RecentLink link, CancellationToken cancellationToken = default)
    {
        string messageId = link.HostId ?? throw new ArgumentException("The link has no Discord message id.", nameof(link));
        Uri address = new($"{ReadWebhook().AbsoluteUri}/messages/{Uri.EscapeDataString(messageId)}");
        using HttpResponseMessage response = await SendWithRetryAsync(
            async () =>
            {
                using HttpRequestMessage request = new(HttpMethod.Delete, address);
                return await SendAsync(request, cancellationToken);
            },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Refused(response.StatusCode);
        }
    }

    private TimeSpan RequestTimeout => TimeSpan.FromSeconds(settings.Discord.TimeoutSeconds);

    private Uri ReadWebhook() =>
        DiscordWebhookAddress.TryParse(credentials.Read(settings.Discord.CredentialName)?.SecretAccessKey, out Uri? webhook)
            ? webhook
            : throw new ShareUploadException(Strings.Get("error.discord.address"));

    /// <summary>Sends once; after a rate limit it waits the time Discord names (at most the timeout) and sends once more.</summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await send();
        if ((int)response.StatusCode != RateLimited)
        {
            return response;
        }

        TimeSpan retryAfter;
        using (response)
        {
            retryAfter = await RetryAfterAsync(response, cancellationToken);
            if (retryAfter > RequestTimeout)
            {
                throw Refused(response.StatusCode);
            }
        }

        await _wait(retryAfter, cancellationToken);
        return await send();
    }

    private ShareUploadException TooBig(long size) => new(Strings.Get(
        "error.discord.size", MegabyteText.FromBytes(size), MegabyteText.FromMegabytes(settings.Discord.MaxUploadMegabytes)));

    private static ShareUploadException Refused(HttpStatusCode status) =>
        new(Strings.Get("error.discord.status", ((int)status).ToString(CultureInfo.InvariantCulture)));

    /// <summary>The name Discord shows in the link: random, so the capture's own file name stays private.</summary>
    private static string RandomName(string filePath) =>
        RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", NameLength) + Path.GetExtension(filePath).ToLowerInvariant();

    private static string PayloadJson(string name) =>
        JsonSerializer.Serialize(new { attachments = new[] { new { id = 0, filename = name } } });

    private async Task<HttpResponseMessage> PostAsync(Uri address, string filePath, string name, CancellationToken cancellationToken)
    {
        await using FileStream file = File.OpenRead(filePath);
        using StreamContent picture = new(file);
        picture.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using StringContent payload = new(PayloadJson(name), System.Text.Encoding.UTF8, "application/json");
        using MultipartFormDataContent form = new() { { picture, "files[0]", name }, { payload, "payload_json" } };
        using HttpRequestMessage request = new(HttpMethod.Post, address) { Content = form };
        return await SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(RequestTimeout);
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShareUploadException(Strings.Get("error.discord.timeout"));
        }
        catch (HttpRequestException)
        {
            throw new ShareUploadException(Strings.Get("error.discord.network"));
        }
    }

    /// <summary>Discord names the wait in the JSON body (seconds, may be fractional); the header is the fallback.</summary>
    private static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (body.RootElement.TryGetProperty("retry_after", out JsonElement seconds) && seconds.TryGetDouble(out double value) && value >= 0)
            {
                return TimeSpan.FromSeconds(value);
            }
        }
        catch (JsonException)
        {
            // not JSON: use the header
        }

        return response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
    }

    private static async Task<ShareLink> ReadLinkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument message = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (message.RootElement.TryGetProperty("attachments", out JsonElement attachments)
                && attachments.ValueKind == JsonValueKind.Array
                && attachments.GetArrayLength() > 0
                && attachments[0].TryGetProperty("url", out JsonElement url)
                && url.ValueKind == JsonValueKind.String
                && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? link)
                && link.Scheme == Uri.UriSchemeHttps)
            {
                string? messageId = message.RootElement.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
                    ? id.GetString()
                    : null;
                return new ShareLink(link, DiscordLinkExpiry.From(link), messageId);
            }
        }
        catch (JsonException)
        {
            // not a message: reported below
        }

        throw new ShareUploadException(Strings.Get("error.discord.reply"));
    }
}
