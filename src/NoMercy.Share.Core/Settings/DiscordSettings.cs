namespace NoMercy.Share.Core.Settings;

/// <summary>Discord as a storage provider. The webhook address is a secret: it lives in the credential store, named here, never in settings.json.</summary>
public sealed record DiscordSettings
{
    /// <summary>The value of <see cref="StorageSettings.Provider"/> that selects Discord.</summary>
    public const string ProviderName = "discord";

    /// <summary>The name of the stored webhook address; the name only, never the address.</summary>
    public string CredentialName { get; init; } = ShareDefaults.DiscordCredentialName;

    /// <summary>How long one post to Discord may take before it counts as failed.</summary>
    public int TimeoutSeconds { get; init; } = ShareDefaults.DiscordTimeoutSeconds;

    /// <summary>The largest file Discord is given, in megabytes. A bigger file fails before the upload.</summary>
    public double MaxUploadMegabytes { get; init; } = ShareDefaults.DiscordMaxUploadMegabytes;
}
