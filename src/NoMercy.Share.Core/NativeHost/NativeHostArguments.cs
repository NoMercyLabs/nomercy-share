namespace NoMercy.Share.Core.NativeHost;

/// <summary>Tells whether the app was started by a browser as a native messaging host.</summary>
public static class NativeHostArguments
{
    private const string OriginPrefix = "chrome-extension://";

    /// <summary>The caller origin a browser passes as the first argument; null when this is a normal start.</summary>
    public static string? Origin(string[] args) =>
        args.Length > 0 && args[0].StartsWith(OriginPrefix, StringComparison.Ordinal) ? args[0] : null;
}
