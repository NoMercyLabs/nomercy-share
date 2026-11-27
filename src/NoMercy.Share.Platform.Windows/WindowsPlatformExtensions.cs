using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Platform.Windows;

public static class WindowsPlatformExtensions
{
    /// <summary>Registers every Windows implementation of the platform interfaces. The clipboard belongs to the app's UI toolkit.</summary>
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.AddSingleton<IWebPageCapture, WindowsWebPageCapture>();
        services.AddSingleton<IGlobalHotkeys, WindowsGlobalHotkeys>();
        return services;
    }
}
