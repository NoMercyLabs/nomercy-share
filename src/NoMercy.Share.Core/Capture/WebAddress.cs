using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Share.Core.Capture;

/// <summary>The addresses a web capture may open: http and https only. file:, javascript:, data: and browser pages are refused.</summary>
public static class WebAddress
{
    public static bool TryParse(string? text, [NotNullWhen(true)] out Uri? address)
    {
        address = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? parsed) || !IsWeb(parsed))
        {
            return false;
        }

        address = parsed;
        return true;
    }

    public static bool IsWeb(Uri address) =>
        (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrEmpty(address.Host);

    public static bool IsHttps(Uri address) => address.Scheme == Uri.UriSchemeHttps;
}
