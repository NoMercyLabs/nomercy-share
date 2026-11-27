using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the bar. The caller registers the settings records (<see cref="StorageSettings"/>, <see cref="ExpirySettings"/>, <see cref="ClipboardSettings"/>),
    /// <see cref="CaptureSettings"/>, <see cref="HotkeySettings"/>, the platform interfaces (with <see cref="IGlobalHotkeys"/>), the <see cref="ICaptureHost"/> and every <see cref="IShareUploader"/>; the uploader named in the storage settings is the one used.
    /// </summary>
    public static IServiceCollection AddShareCore(this IServiceCollection services)
    {
        services.AddSingleton(sp => new ShareFlow(
            ShareUploaderSelector.Select(sp.GetServices<IShareUploader>(), sp.GetRequiredService<StorageSettings>()),
            sp.GetRequiredService<IClipboard>(),
            sp.GetRequiredService<ExpirySettings>(),
            sp.GetRequiredService<ClipboardSettings>()));
        services.AddSingleton(sp => new CaptureFiles(
            sp.GetRequiredService<CaptureSettings>(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            () => DateTimeOffset.UtcNow));
        services.AddSingleton(sp => new CaptureFlow(
            sp.GetRequiredService<IScreenCapture>(),
            sp.GetRequiredService<ICaptureHost>(),
            sp.GetRequiredService<CaptureSettings>()));
        services.AddSingleton<BarViewModel>();
        services.AddSingleton<HotkeyBinder>();
        return services;
    }
}
