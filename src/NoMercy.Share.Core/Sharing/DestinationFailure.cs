namespace NoMercy.Share.Core.Sharing;

/// <summary>A destination that did not get the link.</summary>
/// <param name="Name">The destination's name, for example "Discord".</param>
/// <param name="Reason">A short reason for the user.</param>
public sealed record DestinationFailure(string Name, string Reason);
