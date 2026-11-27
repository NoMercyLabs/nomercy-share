using NoMercy.Share.Core.Sharing;

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

    /// <summary>The signing region. R2 wants "auto".</summary>
    public string Region { get; init; } = ShareDefaults.Region;

    /// <summary>Address the bucket as endpoint/bucket instead of bucket.endpoint. R2 and most self-hosted services accept it.</summary>
    public bool ForcePathStyle { get; init; } = ShareDefaults.ForcePathStyle;

    /// <summary>The address the shared links start with, for example a custom domain in front of the bucket.</summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>The name of the stored credential; the name only, never the secret.</summary>
    public string CredentialName { get; init; } = string.Empty;

    /// <summary>The length of the random id in an object key.</summary>
    public int KeyLength { get; init; } = ShareDefaults.KeyLength;

    /// <summary>The lifetimes, in days, that have a lifecycle rule on the host. An upload goes under the smallest that covers it.</summary>
    public int[] ExpiryBucketDays { get; init; } = [.. ShareDefaults.ExpiryBucketDays];

    /// <summary>The folder of an expiring object: a format string whose {0} is the bucket's days.</summary>
    public string ExpiryPrefixPattern { get; init; } = ShareDefaults.ExpiryPrefixPattern;

    /// <summary>The folder of an object that never expires. No lifecycle rule belongs on it.</summary>
    public string KeepPrefix { get; init; } = ShareDefaults.KeepPrefix;

    /// <summary>Appended to the object key to name the sidecar file that holds the expiry.</summary>
    public string SidecarSuffix { get; init; } = ShareDefaults.SidecarSuffix;

    /// <summary>True once any share-host value is filled in. With none, captures stay local; with some, Validate names what is missing.</summary>
    public bool IsSetUp =>
        !string.IsNullOrWhiteSpace(Endpoint) || !string.IsNullOrWhiteSpace(Bucket)
        || !string.IsNullOrWhiteSpace(PublicBaseUrl) || !string.IsNullOrWhiteSpace(CredentialName);

    /// <summary>The link to an object key.</summary>
    public Uri LinkFor(string key) => new($"{PublicBaseUrl.TrimEnd('/')}/{key}");

    /// <summary>Throws <see cref="InvalidStorageSettingsException"/> naming the first setting that cannot work.</summary>
    public void Validate()
    {
        RequireText(nameof(Endpoint), Endpoint);
        RequireHttpAddress(nameof(Endpoint), Endpoint);
        RequireText(nameof(Bucket), Bucket);
        RequireText(nameof(Region), Region);
        RequireText(nameof(PublicBaseUrl), PublicBaseUrl);
        RequireHttpAddress(nameof(PublicBaseUrl), PublicBaseUrl);
        RequireText(nameof(CredentialName), CredentialName);
        RequireText(nameof(KeepPrefix), KeepPrefix);
        RequireText(nameof(ExpiryPrefixPattern), ExpiryPrefixPattern);

        if (KeyLength is < ShareDefaults.MinKeyLength or > ShareDefaults.MaxKeyLength)
        {
            throw Invalid(nameof(KeyLength), Strings.Get("error.setting.range", ShareDefaults.MinKeyLength, ShareDefaults.MaxKeyLength));
        }

        if (ExpiryBucketDays is not { Length: > 0 } || ExpiryBucketDays.Any(days => days < 1))
        {
            throw Invalid(nameof(ExpiryBucketDays), Strings.Get("error.setting.days"));
        }
    }

    private static InvalidStorageSettingsException Invalid(string name, string reason) =>
        new($"Storage.{name}", Strings.Get("error.setting.invalid", $"Storage.{name}", reason));

    private static void RequireText(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidStorageSettingsException($"Storage.{name}", Strings.Get("error.setting.missing", $"Storage.{name}"));
        }
    }

    private static void RequireHttpAddress(string name, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            throw Invalid(name, Strings.Get("error.setting.address"));
        }
    }
}
