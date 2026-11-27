using System.Globalization;

namespace NoMercy.Share.Storage.Discord;

/// <summary>Discord signs its file links and puts the end of their life in the query as ex, hex Unix seconds.</summary>
internal static class DiscordLinkExpiry
{
    private const string Key = "ex=";

    /// <summary>The expiry in the link; null when it has none or it cannot be read. Unknown is never "never".</summary>
    public static DateTimeOffset? From(Uri link)
    {
        foreach (string part in link.Query.TrimStart('?').Split('&'))
        {
            if (part.StartsWith(Key, StringComparison.Ordinal))
            {
                return long.TryParse(part[Key.Length..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long seconds)
                    && seconds is > 0 and < 253402300799
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                    : null;
            }
        }

        return null;
    }
}
