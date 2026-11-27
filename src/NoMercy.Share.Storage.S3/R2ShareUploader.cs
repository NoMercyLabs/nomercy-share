using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3;

/// <summary>Uploads to Cloudflare R2 through its S3-compatible API. Not built yet: the upload step fills this in.</summary>
public sealed class R2ShareUploader : IShareUploader
{
    public const string Name = "r2";

    public string ProviderName => Name;

    public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The R2 upload arrives in the upload step.");
}

public static class S3StorageExtensions
{
    /// <summary>Registers the R2 provider; the storage settings select it by the name "r2".</summary>
    public static IServiceCollection AddR2Storage(this IServiceCollection services)
    {
        services.AddSingleton<IShareUploader, R2ShareUploader>();
        return services;
    }
}
