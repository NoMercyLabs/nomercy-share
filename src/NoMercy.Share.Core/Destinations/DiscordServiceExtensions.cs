using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Destinations;

public static class DiscordServiceExtensions
{
    /// <summary>Each post has its own timeout from the settings, so the client has none of its own.</summary>
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Registers the Discord destination. The caller registers <see cref="LiveSettings"/> and <see cref="ICredentialStore"/>.</summary>
    public static IServiceCollection AddDiscordDestination(this IServiceCollection services) =>
        services.AddSingleton<ILinkDestination>(sp =>
        {
            LiveSettings live = sp.GetRequiredService<LiveSettings>();
            return new DiscordWebhookDestination(Client, () => live.Current.Discord, sp.GetRequiredService<ICredentialStore>());
        });
}
