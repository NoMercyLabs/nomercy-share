using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.NativeHost;

/// <summary>How the app answers the browser extension: the biggest message, who may ask, and whether the browsers are told where the app is.</summary>
public sealed record NativeHostSettings
{
    /// <summary>The biggest message the app reads from the extension, in megabytes. A bigger one gets an error reply.</summary>
    public int MaxMessageMegabytes { get; init; } = ShareDefaults.NativeHostMaxMessageMegabytes;

    /// <summary>The extension origins that are served, for example chrome-extension://id/. Any other origin gets an error reply.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [ShareDefaults.ExtensionOrigin];

    /// <summary>True when the app writes its native messaging manifest and browser keys at start.</summary>
    public bool Register { get; init; } = ShareDefaults.NativeHostRegister;
}
