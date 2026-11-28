using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

public static class WindowsPlatformExtensions
{
    /// <summary>GitHub's API refuses a call without a User-Agent; the 300 MB download has no time limit of its own.</summary>
    private static readonly HttpClient DownloadClient = CreateDownloadClient();

    private static HttpClient CreateDownloadClient()
    {
        HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NoMercy-Share");
        return client;
    }

    /// <summary>Registers the Windows screen capture, screen recording, web page capture, global hotkeys, https opener, foreground grant, the capture exclusion and input pass-through of its windows, and credential store.</summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture>(sp => new WindowsScreenCapture(
            sp.GetRequiredService<CaptureFiles>(), sp.GetRequiredService<IRegionSelector>(), sp.GetRequiredService<CaptureSettings>()));
        services.AddSingleton<IScreenRecorder>(sp => new FfmpegScreenRecorder(
            sp.GetRequiredService<CaptureFiles>(), sp.GetRequiredService<IRegionSelector>(),
            () => sp.GetRequiredService<LiveSettings>().Current.EffectiveRecording()));
        services.AddSingleton<IWebPageCapture>(sp => new CdpWebPageCapture(
            sp.GetRequiredService<CaptureFiles>(), () => sp.GetRequiredService<LiveSettings>().Current.WebCapture));
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IGlobalHotkeys, Win32GlobalHotkeys>();
        services.AddSingleton<IUrlOpener>(new ShellUrlOpener());
        services.AddSingleton<IForegroundGrant, WindowsForegroundGrant>();
        services.AddSingleton<ICaptureExclusion, WindowsCaptureExclusion>();
        services.AddSingleton<IInputPassThrough, WindowsInputPassThrough>();
        services.AddSingleton<IFolderAccess, WindowsFolderAccess>();
        services.AddSingleton<IEncoderProbe>(sp => new FfmpegEncoderProbe(() => sp.GetRequiredService<LiveSettings>().Current.Recording));
        services.AddSingleton<IFfmpegInstall>(sp => new FfmpegDownload(DownloadClient, FfmpegDownload.DefaultFolder(), ShareDefaults.FfmpegReleaseUrl, FfmpegDownload.ThisPlatform(),
            () => FfmpegLocator.Find(sp.GetRequiredService<LiveSettings>().Current.Recording.FfmpegPath, Environment.GetEnvironmentVariable("PATH"), FfmpegLocator.NoMercyBuilds()) is not null));
        return services;
    }
}
