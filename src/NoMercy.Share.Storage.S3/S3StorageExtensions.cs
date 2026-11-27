using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3;

public static class S3StorageExtensions
{
    /// <summary>Registers the S3-compatible uploader once for each name in <see cref="S3ShareUploader.ProviderNames"/>; the storage settings select one.</summary>
    public static IServiceCollection AddS3Storage(this IServiceCollection services)
    {
        foreach (string name in S3ShareUploader.ProviderNames)
        {
            services.AddSingleton<IShareUploader>(sp => new S3ShareUploader(
                name, sp.GetRequiredService<StorageSettings>(), sp.GetRequiredService<ICredentialStore>()));
        }

        return services;
    }
}
