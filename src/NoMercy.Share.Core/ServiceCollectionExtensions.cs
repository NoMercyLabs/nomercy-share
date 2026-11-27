using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the bar. The caller registers the settings records (<see cref="StorageSettings"/>, <see cref="ExpirySettings"/>),
    /// the platform interfaces and every <see cref="IShareUploader"/>; the uploader named in the storage settings is the one used.
    /// </summary>
    public static IServiceCollection AddShareCore(this IServiceCollection services)
    {
        services.AddSingleton(sp => new ShareFlow(
            ShareUploaderSelector.Select(sp.GetServices<IShareUploader>(), sp.GetRequiredService<StorageSettings>()),
            sp.GetRequiredService<IClipboard>(),
            sp.GetRequiredService<ExpirySettings>()));
        services.AddSingleton<BarViewModel>();
        return services;
    }
}
