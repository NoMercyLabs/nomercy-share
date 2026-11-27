namespace NoMercy.Share.Core.NativeHost;

/// <summary>A frame that announces more bytes than <see cref="NativeHostSettings.MaxMessageMegabytes"/> allows.</summary>
public sealed class NativeMessageTooLargeException(string message) : Exception(message);
