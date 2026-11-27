using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Share.Core.Discord;

/// <summary>What counts as a Discord webhook address, and how it sits in the credential store.</summary>
public static class DiscordWebhookAddress
{
    /// <summary>The key id of the stored credential; the address is its secret part.</summary>
    public const string CredentialUser = "webhook";

    private const string PathStart = "/api/webhooks/";

    private static readonly string[] Hosts = ["discord.com", "discordapp.com", "canary.discord.com", "ptb.discord.com"];

    /// <summary>True for an https address on a Discord host whose path starts with /api/webhooks/ and goes on from there.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Uri? address)
    {
        address = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? candidate))
        {
            return false;
        }

        bool allowed = candidate.Scheme == Uri.UriSchemeHttps
            && candidate.IsDefaultPort
            && string.IsNullOrEmpty(candidate.UserInfo)
            && Hosts.Contains(candidate.Host, StringComparer.OrdinalIgnoreCase)
            && candidate.AbsolutePath.StartsWith(PathStart, StringComparison.Ordinal)
            && candidate.AbsolutePath.Length > PathStart.Length;
        if (allowed)
        {
            address = candidate;
        }

        return allowed;
    }

    public static StoredCredential ToCredential(string address) => new(CredentialUser, address.Trim());
}
