namespace NoMercy.Share.Core.Sharing;

/// <summary>A link that could not be sent to a destination. The message is a short reason for the user and never holds a secret.</summary>
public sealed class LinkDestinationException(string message) : Exception(message);
