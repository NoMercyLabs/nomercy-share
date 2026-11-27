using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires the bar. The caller registers <see cref="LiveSettings"/> (storage, expiry), <see cref="ClipboardSettings"/>,
    /// <see cref="CaptureSettings"/>, <see cref="HotkeySettings"/>, the platform interfaces (with <see cref="IGlobalHotkeys"/>), the <see cref="ICaptureHost"/> and every <see cref="IShareUploader"/>; the uploader named in the storage settings is the one used.
    /// </summary>
    public static IServiceCollection AddShareCore(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            LiveSettings live = sp.GetRequiredService<LiveSettings>();
            IShareUploader Uploader() => ShareUploaderSelector.Select(sp.GetServices<IShareUploader>(), live.Current.Storage);
            _ = Uploader(); // a provider name nobody answers to fails when the bar is built, as before
            return new ShareFlow(
                Uploader,
                sp.GetRequiredService<IClipboard>(),
                () => live.Current.Expiry,
                sp.GetRequiredService<ClipboardSettings>(),
                sp.GetService<RecentLinks>());
        });
        services.AddSingleton(sp => new CaptureFiles(
            sp.GetRequiredService<CaptureSettings>(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            () => DateTimeOffset.UtcNow));
        services.AddSingleton(sp => new CaptureFlow(
            sp.GetRequiredService<IScreenCapture>(),
            sp.GetRequiredService<ICaptureHost>(),
            sp.GetRequiredService<CaptureSettings>()));
        services.TryAddSingleton<IFolderAccess, NoFolderAccess>();
        services.AddSingleton<BarViewModel>();
        services.AddSingleton<HotkeyBinder>();
        return services;
    }
}
