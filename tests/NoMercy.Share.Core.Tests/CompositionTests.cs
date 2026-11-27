using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Tests;

public sealed class CompositionTests
{
    private static ServiceProvider Build(ShareSettings settings, params IShareUploader[] uploaders)
    {
        ServiceCollection services = new();
        services.AddSingleton(settings.Storage);
        services.AddSingleton(settings.Expiry);
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
    public async Task TheUploaderTheSettingsNameIsTheOneTheBarUses()
    {
        FakeUploader r2 = new("r2");
        FakeUploader other = new("minio");
        using ServiceProvider provider = Build(new ShareSettings { Storage = new StorageSettings { Provider = "minio" } }, r2, other);

        BarViewModel bar = provider.GetRequiredService<BarViewModel>();
        await bar.RunAsync(BarAction.WebPage);

        Assert.Empty(r2.Uploads);
        Assert.Single(other.Uploads);
    }

    [Fact]
    public void AnUnknownProviderInTheSettingsFailsWhenTheBarIsBuilt()
    {
        using ServiceProvider provider = Build(new ShareSettings { Storage = new StorageSettings { Provider = "nope" } }, new FakeUploader("r2"));

        Assert.Throws<UnknownStorageProviderException>(() => provider.GetRequiredService<BarViewModel>());
    }
}
