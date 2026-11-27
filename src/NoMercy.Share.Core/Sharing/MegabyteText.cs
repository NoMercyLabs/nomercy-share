using System.Globalization;

namespace NoMercy.Share.Core.Sharing;

/// <summary>A size in megabytes as text for a notice: one decimal at most, in the invariant culture.</summary>
public static class MegabyteText
{
    private const double BytesPerMegabyte = 1024.0 * 1024;

    public static string FromBytes(long bytes) => FromMegabytes(bytes / BytesPerMegabyte);

    public static string FromMegabytes(double megabytes) => megabytes.ToString("0.#", CultureInfo.InvariantCulture);
}
