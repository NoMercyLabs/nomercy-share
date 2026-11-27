using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

public static class WindowsPlatformExtensions
{
    /// <summary>Registers the Windows screen capture, screen recording, global hotkeys, https opener and credential store. Web page capture is not built.</summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture>(sp => new WindowsScreenCapture(
            sp.GetRequiredService<CaptureFiles>(), sp.GetRequiredService<IRegionSelector>(), sp.GetRequiredService<CaptureSettings>()));
        services.AddSingleton<IScreenRecorder>(sp => new FfmpegScreenRecorder(
            sp.GetRequiredService<CaptureFiles>(), sp.GetRequiredService<IRegionSelector>(),
            () => sp.GetRequiredService<LiveSettings>().Current.Recording));
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IGlobalHotkeys, Win32GlobalHotkeys>();
        services.AddSingleton<IUrlOpener>(new ShellUrlOpener());
        return services;
    }
}
