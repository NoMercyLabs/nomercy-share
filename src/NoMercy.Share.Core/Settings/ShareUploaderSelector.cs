using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Settings;

/// <summary>A storage provider name in the settings that no registered uploader answers to.</summary>
public sealed class UnknownStorageProviderException(string message) : Exception(message);

/// <summary>Picks the registered uploader whose name the storage settings give.</summary>
public static class ShareUploaderSelector
{
    public static IShareUploader Select(IEnumerable<IShareUploader> uploaders, StorageSettings settings)
    {
        List<IShareUploader> known = [.. uploaders];
        IShareUploader? chosen = known.FirstOrDefault(u => string.Equals(u.ProviderName, settings.Provider, StringComparison.OrdinalIgnoreCase));
        return chosen ?? throw new UnknownStorageProviderException(
            Strings.Get("error.provider.unknown", settings.Provider, string.Join(", ", known.Select(u => u.ProviderName))));
    }
}
