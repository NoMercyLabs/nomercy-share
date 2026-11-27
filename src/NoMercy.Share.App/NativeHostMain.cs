using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Platform.Windows;
using NoMercy.Share.Storage.Discord;
using NoMercy.Share.Storage.S3;

namespace NoMercy.Share.App;

/// <summary>The app as a native messaging host: no window, no hotkeys, no tray. It answers one message from the extension and ends.</summary>
internal static class NativeHostMain
{
    public static int Run(string origin)
    {
        ShareSettings settings = new SettingsFile(SettingsFile.DefaultPath()).Load();
        ServiceCollection services = new();
        services.AddSingleton(new LiveSettings(settings));
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddS3Storage();
        services.AddDiscordStorage();
        using ServiceProvider provider = services.BuildServiceProvider();
        NativeShareRequestHandler handler = new(
            settings.NativeHost,
            new CaptureFiles(settings.Capture, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), () => DateTimeOffset.UtcNow),
            () => ShareUploaderSelector.Select(provider.GetServices<IShareUploader>(), settings.Storage),
            () => settings.Expiry,
            new RecentLinks(RecentLinks.DefaultPath(), settings.Recent));
        NativeHostRunner runner = new(new NativeMessageCodec(settings.NativeHost), handler);
        runner.RunAsync(origin, Console.OpenStandardInput(), Console.OpenStandardOutput()).GetAwaiter().GetResult();
        return 0;
    }
}
