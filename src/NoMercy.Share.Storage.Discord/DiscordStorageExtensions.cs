using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.Discord;

public static class DiscordStorageExtensions
{
    /// <summary>Each upload has its own timeout from the settings, so the client has none of its own.</summary>
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Registers the "discord" provider, built anew on each lookup from the live settings. The caller registers <see cref="LiveSettings"/> and <see cref="ICredentialStore"/>.</summary>
    public static IServiceCollection AddDiscordStorage(this IServiceCollection services) =>
        services.AddTransient<IShareUploader>(sp => new DiscordShareUploader(
            Client, sp.GetRequiredService<LiveSettings>().Current, sp.GetRequiredService<ICredentialStore>()));
}
