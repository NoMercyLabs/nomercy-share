namespace NoMercy.Share.Core;

/// <summary>An operating system feature that can stop this app from writing in a folder, such as Windows controlled folder access.</summary>
public interface IFolderAccess
{
    /// <summary>True when the system protects folders from apps it does not know.</summary>
    bool IsProtectionOn();

    /// <summary>Asks the system to let this app write in protected folders. False when the user declined or it failed.</summary>
    Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default);
}

/// <summary>For a platform with no such protection.</summary>
public sealed class NoFolderAccess : IFolderAccess
{
    public bool IsProtectionOn() => false;

    public Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}
