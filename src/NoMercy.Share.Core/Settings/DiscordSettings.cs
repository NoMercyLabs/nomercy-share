namespace NoMercy.Share.Core.Settings;

/// <summary>Posting new share links to a Discord channel. The webhook address is a secret: it lives in the credential store, named here, never in settings.json.</summary>
public sealed record DiscordSettings
{
    public bool Enabled { get; init; }

    /// <summary>The name of the stored webhook address; the name only, never the address.</summary>
    public string CredentialName { get; init; } = ShareDefaults.DiscordCredentialName;

    public int TimeoutSeconds { get; init; } = ShareDefaults.DiscordTimeoutSeconds;
}
