namespace NoMercy.Share.Core.Sharing;

/// <summary>What sharing a capture did.</summary>
/// <param name="Link">The public link; null when the capture stayed local because no share host is set up.</param>
/// <param name="Copied">What was put on the clipboard.</param>
public sealed record ShareOutcome(ShareLink? Link, ClipboardPlan Copied);
