namespace NoMercy.Share.Core.Settings;

/// <summary>
/// Where uploads go. The credentials are named here and read from the credential store of the platform;
/// a secret value never lives in this file.
/// </summary>
public sealed record StorageSettings
{
    /// <summary>The provider name, matched against <see cref="Sharing.IShareUploader.ProviderName"/>.</summary>
    public string Provider { get; init; } = ShareDefaults.ProviderName;

    /// <summary>The API endpoint of the storage service.</summary>
    public string Endpoint { get; init; } = string.Empty;

    public string Bucket { get; init; } = string.Empty;

    /// <summary>The address the shared links start with, for example a custom domain in front of the bucket.</summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>The name of the stored credential; the name only, never the secret.</summary>
    public string CredentialName { get; init; } = string.Empty;
}
