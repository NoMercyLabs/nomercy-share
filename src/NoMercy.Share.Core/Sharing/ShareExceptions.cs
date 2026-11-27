namespace NoMercy.Share.Core.Sharing;

/// <summary>An upload that did not happen. The message is the reason for the user and never holds a secret.</summary>
public class ShareUploadException : Exception
{
    public ShareUploadException(string message)
        : base(message)
    {
    }

    public ShareUploadException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>A storage setting that is missing or cannot work. <see cref="Setting"/> is its name, for example "Storage.Bucket".</summary>
public sealed class InvalidStorageSettingsException(string setting, string message) : ShareUploadException(message)
{
    public string Setting { get; } = setting;
}
