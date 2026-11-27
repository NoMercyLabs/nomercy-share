using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;
using NoMercy.Share.Storage.S3;

namespace NoMercy.Share.App;

/// <summary>The composition root: picks the platform module, registers the providers and opens the bar.</summary>
public sealed class App : Application
{
    private BarWindow? _window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ServiceProvider services = BuildServices(() => _window);
            _window = new BarWindow(services.GetRequiredService<BarViewModel>(), services.GetRequiredService<BarPositionStore>(),
                services.GetRequiredService<ShareSettings>().Bar);
            desktop.MainWindow = _window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices(Func<TopLevel?> owner)
    {
        SettingsFile file = new(SettingsFile.DefaultPath());
        ShareSettings settings = file.Load();
        ServiceCollection services = new();
        services.AddSingleton(file);
        services.AddSingleton(settings);
        services.AddSingleton(settings.Storage);
        services.AddSingleton(settings.Expiry);
        services.AddSingleton(settings.Capture);
        services.AddSingleton<BarPositionStore>();
        services.AddSingleton<Core.IClipboard>(new AvaloniaClipboard(owner));
        services.AddSingleton<IRegionSelector>(new AvaloniaRegionSelector(owner));
        services.AddSingleton<ICaptureHost>(new BarWindowHost(() => owner() as BarWindow));
        AddPlatform(services);
        services.AddR2Storage();
        services.AddShareCore();
        return services.BuildServiceProvider();
    }

    /// <summary>The platform module is chosen by operating system; each new desktop adds a branch here.</summary>
    private static void AddPlatform(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddWindowsPlatform();
            return;
        }

        throw new PlatformNotSupportedException(Strings.Get("unsupported.os"));
    }
}
