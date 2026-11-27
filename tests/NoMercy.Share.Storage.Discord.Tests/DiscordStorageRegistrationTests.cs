using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.Discord.Tests;

public sealed class DiscordStorageRegistrationTests
{
    private static ServiceProvider Build(ShareSettings settings)
    {
        ServiceCollection services = new();
        services.AddSingleton(new LiveSettings(settings));
        services.AddSingleton<ICredentialStore>(new FakeCredentialStore());
        services.AddDiscordStorage();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void TheSelectorPicksDiscordByName()
    {
        using ServiceProvider provider = Build(new ShareSettings { Storage = new StorageSettings { Provider = "discord" } });

        IShareUploader chosen = ShareUploaderSelector.Select(provider.GetServices<IShareUploader>(), new StorageSettings { Provider = "Discord" });

        Assert.IsType<DiscordShareUploader>(chosen);
    }

    [Fact]
    public void ANewLookupSeesTheSettingsSavedSinceTheLast()
    {
        ShareSettings first = new() { Storage = new StorageSettings { Provider = "discord" } };
        ServiceCollection services = new();
        LiveSettings live = new(first);
        services.AddSingleton(live);
        services.AddSingleton<ICredentialStore>(new FakeCredentialStore());
        services.AddDiscordStorage();
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IShareUploader>().IsConfigured);

        ((FakeCredentialStore)provider.GetRequiredService<ICredentialStore>()).Stored[ShareDefaults.DiscordCredentialName] =
            new StoredCredential("webhook", "https://discord.com/api/webhooks/000/made-up-token");

        Assert.True(provider.GetRequiredService<IShareUploader>().IsConfigured);
    }
}
