namespace NoMercy.Share.Core.Capture;

/// <summary>The part of a page to take a picture of, in CSS pixels from the top left of the page.</summary>
public readonly record struct WebClip(double X, double Y, double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The whole content, cut off at <paramref name="maxHeight"/>.</summary>
    public static WebClip FullPage(double contentWidth, double contentHeight, double maxHeight) =>
        new(0, 0, contentWidth, Math.Min(contentHeight, maxHeight));

    /// <summary>An element's box as the browser reports it (from the visible top left), moved by the scroll offset to page coordinates.</summary>
    public static WebClip Element(double left, double top, double width, double height, double scrollX, double scrollY, double maxHeight) =>
        new(left + scrollX, top + scrollY, width, Math.Min(height, maxHeight));
}
