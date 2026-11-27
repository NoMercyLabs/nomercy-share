namespace NoMercy.Share.Core.Capture;

/// <summary>A recording could not start, stop or be squeezed. The message is plain text for the bar.</summary>
public sealed class RecordingException(string message) : Exception(message);
