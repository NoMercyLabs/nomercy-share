using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3;

public static class S3StorageExtensions
{
    /// <summary>One uploader per name in <see cref="S3ShareUploader.ProviderNames"/>, built anew on each lookup from the live settings.</summary>
    public static IServiceCollection AddS3Storage(this IServiceCollection services)
    {
        foreach (string name in S3ShareUploader.ProviderNames)
        {
            services.AddTransient<IShareUploader>(sp => new S3ShareUploader(
                name, sp.GetRequiredService<LiveSettings>().Current.Storage, sp.GetRequiredService<ICredentialStore>()));
        }

        return services;
    }
}
