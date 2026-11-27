using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Destinations;

/// <summary>Posts the share link as a message to a Discord channel through its webhook. Discord never gets the file. The address is read from the credential store for each post and is never logged.</summary>
public sealed class DiscordWebhookDestination(
    HttpClient http, Func<DiscordSettings> settings, ICredentialStore credentials, Func<TimeSpan, CancellationToken, Task>? wait = null) : ILinkDestination
{
    private const int RateLimited = 429;

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(1);

    private readonly Func<TimeSpan, CancellationToken, Task> _wait = wait ?? Task.Delay;

    public string Name => "Discord";

    public bool IsConfigured => settings().Enabled && credentials.Read(settings().CredentialName) is not null;

    /// <summary>Posts once; after a rate limit it waits the time Discord names (at most the timeout) and posts once more.</summary>
    public async Task SendAsync(ShareLink link, string contentType, CancellationToken cancellationToken = default)
    {
        DiscordSettings current = settings();
        string? stored = credentials.Read(current.CredentialName)?.SecretAccessKey;
        if (!DiscordWebhookAddress.TryParse(stored, out Uri? address))
        {
            throw new LinkDestinationException(Strings.Get("error.discord.address"));
        }

        string body = JsonSerializer.Serialize(new Dictionary<string, string> { ["content"] = link.Url.AbsoluteUri });
        TimeSpan timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        HttpResponseMessage first = await PostAsync(address, body, timeout, cancellationToken);
        using (first)
        {
            if (first.IsSuccessStatusCode)
            {
                return;
            }

            if ((int)first.StatusCode != RateLimited)
            {
                throw Refused(first.StatusCode);
            }

            TimeSpan retryAfter = await RetryAfterAsync(first, cancellationToken);
            if (retryAfter > timeout)
            {
                throw Refused(first.StatusCode);
            }

            await _wait(retryAfter, cancellationToken);
        }

        using HttpResponseMessage second = await PostAsync(address, body, timeout, cancellationToken);
        if (!second.IsSuccessStatusCode)
        {
            throw Refused(second.StatusCode);
        }
    }

    private static LinkDestinationException Refused(HttpStatusCode status) =>
        new(Strings.Get("error.discord.status", ((int)status).ToString(CultureInfo.InvariantCulture)));

    private async Task<HttpResponseMessage> PostAsync(Uri address, string body, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        using HttpRequestMessage request = new(HttpMethod.Post, address) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LinkDestinationException(Strings.Get("error.discord.timeout"));
        }
        catch (HttpRequestException)
        {
            throw new LinkDestinationException(Strings.Get("error.discord.network"));
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
}
