namespace NoMercy.Share.Core.NativeHost;

/// <summary>An input that ended inside a frame.</summary>
public sealed class NativeMessageTruncatedException(string message) : Exception(message);
