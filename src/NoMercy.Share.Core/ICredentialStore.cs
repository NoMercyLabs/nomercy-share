namespace NoMercy.Share.Core;

/// <summary>A key pair read from the credential store. It lives in memory only; never log it.</summary>
public sealed record StoredCredential(string AccessKeyId, string SecretAccessKey)
{
    public override string ToString() => $"{nameof(StoredCredential)} {{ AccessKeyId = {AccessKeyId}, SecretAccessKey = *** }}";
}

/// <summary>The credential store of the platform. settings.json holds the credential's name, never its value.</summary>
public interface ICredentialStore
{
    /// <summary>The credential with this name, or null when there is none.</summary>
    StoredCredential? Read(string name);
}
