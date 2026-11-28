using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Bar;

/// <summary>A finished capture that waits for the user to say what happens with it. The file is already saved.</summary>
public sealed record CaptureChoice(CaptureResult Capture)
{
    /// <summary>Only a picture can go on the clipboard as itself.</summary>
    public bool CanCopyImage => Capture.ContentType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>The choice is only offered with a share host set up, so a link is always possible.</summary>
    public bool CanShareLink => true;

    public string Text => Strings.Get(CanCopyImage ? "choice.picture" : "choice.video");
}
