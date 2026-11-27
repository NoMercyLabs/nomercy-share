namespace NoMercy.Share.Core.NativeHost;

/// <summary>What the extension sends: a picture of a page and how it was taken.</summary>
/// <param name="Type">Always "share".</param>
/// <param name="PngBase64">The PNG file, base64 encoded.</param>
/// <param name="PageUrl">The address of the page; kept for the record, not used to share.</param>
/// <param name="Mode">fullPage, visible or element.</param>
public sealed record NativeShareRequest(string? Type, string? PngBase64, string? PageUrl, string? Mode);
