using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class CompositionTests
{
    private static ServiceProvider Build(ShareSettings settings, params IShareUploader[] uploaders)
    {
        ServiceCollection services = new();
        services.AddSingleton(new LiveSettings(settings));
        services.AddSingleton(settings.Clipboard);
        services.AddSingleton(settings.Capture with { HideDelayMilliseconds = 0 });
        services.AddSingleton<ICaptureHost>(new FakeCaptureHost());
        services.AddSingleton<IScreenCapture>(new FakeScreenCapture());
        services.AddSingleton<IScreenRecorder>(new FakeScreenRecorder());
        services.AddSingleton<IWebPageCapture>(new FakeWebPageCapture());
        services.AddSingleton<IClipboard>(new FakeClipboard());
        foreach (IShareUploader uploader in uploaders)
        {
            services.AddSingleton(uploader);
        }

        services.AddShareCore();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ALinkCanBeDeletedByAProviderThatIsNotTheSelectedOne()
    {
        FakeUploader earlier = new("r2") { Deletes = true };
        using ServiceProvider provider = Build(
            new ShareSettings { Storage = new StorageSettings { Provider = "minio" } }, earlier, new FakeUploader("minio"));
        RecentLink link = new("https://share.example.test/a.png", RecentKind.Image, "image/png", 10, DateTimeOffset.UnixEpoch, null, null);

        Assert.True(provider.GetRequiredService<ShareDeletion>().CanDeleteFromHost(link));
    }

    [Fact]
    public async Task TheUploaderTheSettingsNameIsTheOneTheBarUses()
    {
        FakeUploader r2 = new("r2");
        FakeUploader other = new("minio");
        using ServiceProvider provider = Build(new ShareSettings { Storage = new StorageSettings { Provider = "minio" } }, r2, other);

        BarViewModel bar = provider.GetRequiredService<BarViewModel>();
        await bar.CaptureWebPageAsync();
        await bar.ShareLinkAsync();

        Assert.Empty(r2.Uploads);
        Assert.Single(other.Uploads);
    }

    [Fact]
    public async Task NewStorageAndExpirySettingsAreUsedByTheNextShare()
    {
        FakeUploader r2 = new("r2");
        FakeUploader other = new("minio");
        using ServiceProvider provider = Build(new ShareSettings { Expiry = new ExpirySettings { DefaultDays = 7 } }, r2, other);
        BarViewModel bar = provider.GetRequiredService<BarViewModel>();
        await bar.CaptureWebPageAsync();
        await bar.ShareLinkAsync();

        provider.GetRequiredService<LiveSettings>().Replace(new ShareSettings
        {
            Storage = new StorageSettings { Provider = "minio" },
            Expiry = new ExpirySettings { DefaultDays = null }
        });
        await bar.CaptureWebPageAsync();
        await bar.ShareLinkAsync();

        Assert.Equal(7, Assert.Single(r2.Uploads).Expiry.Lifetime?.TotalDays);
        Assert.Null(Assert.Single(other.Uploads).Expiry.Lifetime);
    }

    [Fact]
    public async Task TheSettingsButtonAsksTheWindowToOpenAndNeverShowsTheLaterNotice()
    {
        using ServiceProvider provider = Build(new ShareSettings(), new FakeUploader());
        BarViewModel bar = provider.GetRequiredService<BarViewModel>();
        int requests = 0;
        bar.SettingsRequested += (_, _) => requests++;

        await bar.RunAsync(BarAction.Settings);

        Assert.Equal(1, requests);
        Assert.Null(bar.Notice);
    }

    [Fact]
    public void AnUnknownProviderInTheSettingsFailsWhenTheBarIsBuilt()
    {
        using ServiceProvider provider = Build(new ShareSettings { Storage = new StorageSettings { Provider = "nope" } }, new FakeUploader("r2"));

        Assert.Throws<UnknownStorageProviderException>(() => provider.GetRequiredService<BarViewModel>());
    }
}
