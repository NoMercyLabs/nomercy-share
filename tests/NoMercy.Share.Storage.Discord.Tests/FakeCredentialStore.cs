using NoMercy.Share.Core;

namespace NoMercy.Share.Storage.Discord.Tests;

internal sealed class FakeCredentialStore : ICredentialStore
{
    public Dictionary<string, StoredCredential> Stored { get; } = [];

    public StoredCredential? Read(string name) => Stored.GetValueOrDefault(name);

    public void Write(string name, StoredCredential credential) => Stored[name] = credential;
}
