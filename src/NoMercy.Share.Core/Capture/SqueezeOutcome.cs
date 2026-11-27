namespace NoMercy.Share.Core.Capture;

/// <summary>How the squeeze ended: the plan of the last encode, its file size in bytes, the retries used, and whether it is still over the cap.</summary>
public readonly record struct SqueezeOutcome(VideoPlan Plan, long Bytes, int Retries, bool OverCap);
