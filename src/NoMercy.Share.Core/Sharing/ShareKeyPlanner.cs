using System.Globalization;
using System.Security.Cryptography;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Sharing;

/// <summary>Where an upload goes: the object key, the sidecar key and when the host deletes the object.</summary>
public sealed record SharePlan(string Key, string SidecarKey, DateTimeOffset? ExpiresAt);

/// <summary>
/// Makes the object key of an upload: the expiry folder, a random id and the file extension. The local file name
/// never reaches the key, since it can hold personal text.
/// </summary>
public sealed class ShareKeyPlanner(StorageSettings settings, Func<int, int>? nextIndex = null)
{
    private readonly Func<int, int> _nextIndex = nextIndex ?? RandomNumberGenerator.GetInt32;

    public SharePlan Plan(Expiry expiry, DateTimeOffset now, string filePath)
    {
        string folder;
        DateTimeOffset? expiresAt;
        if (expiry.Lifetime is { } lifetime)
        {
            int days = BucketDays(lifetime);
            folder = string.Format(CultureInfo.InvariantCulture, settings.ExpiryPrefixPattern, days);
            expiresAt = now.AddDays(days);
        }
        else
        {
            folder = settings.KeepPrefix;
            expiresAt = null;
        }

        string key = $"{folder}/{NewId()}{Extension(filePath)}";
        return new SharePlan(key, key + settings.SidecarSuffix, expiresAt);
    }

    private int BucketDays(TimeSpan lifetime)
    {
        int[] buckets = [.. settings.ExpiryBucketDays.Order()];
        double needed = lifetime.TotalDays;
        foreach (int days in buckets)
        {
            if (days >= needed)
            {
                return days;
            }
        }

        return buckets[^1];
    }

    private string NewId()
    {
        char[] id = new char[settings.KeyLength];
        for (int i = 0; i < id.Length; i++)
        {
            id[i] = ShareDefaults.KeyAlphabet[_nextIndex(ShareDefaults.KeyAlphabet.Length)];
        }

        return new string(id);
    }

    private static string Extension(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        bool safe = extension.Length is > 1 and <= ShareDefaults.MaxExtensionLength
            && extension.Skip(1).All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');
        return safe ? extension : string.Empty;
    }
}
