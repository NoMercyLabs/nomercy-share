using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Platform.Windows;

public static class WindowsPlatformExtensions
{
    /// <summary>Registers the Windows screen capture and credential store. Recording and web page capture are not built.</summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture>(sp => new WindowsScreenCapture(
            sp.GetRequiredService<CaptureFiles>(), sp.GetRequiredService<IRegionSelector>(), sp.GetRequiredService<CaptureSettings>()));
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        return services;
    }
}
