namespace NoMercy.Share.Core.Sharing;

/// <summary>An upload or a delete that the host did not do. The message is the reason for the user and never holds a secret.</summary>
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

/// <summary>Why a storage setting cannot work; the settings window words each one for its own field.</summary>
public enum StorageProblem
{
    Other,
    Missing,
    Address
}

/// <summary>A storage setting that is missing or cannot work. <see cref="Setting"/> is its name, for example "Storage.Bucket".</summary>
public sealed class InvalidStorageSettingsException(string setting, string message, StorageProblem problem = StorageProblem.Other) : ShareUploadException(message)
{
    public string Setting { get; } = setting;

    public StorageProblem Problem { get; } = problem;
}
