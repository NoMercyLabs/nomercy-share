using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Platform.Windows;
using NoMercy.Share.Storage.S3;

namespace NoMercy.Share.App;

/// <summary>The composition root: picks the platform module, registers the providers and opens the bar.</summary>
public sealed class App : Application
{
    private BarWindow? _window;
    private SettingsWindow? _settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ServiceProvider services = BuildServices(() => _window);
            _window = new BarWindow(services.GetRequiredService<BarViewModel>(), services.GetRequiredService<BarPositionStore>(),
                services.GetRequiredService<ShareSettings>().Bar);
            desktop.MainWindow = _window;
            services.GetRequiredService<BarViewModel>().SettingsRequested += (_, _) => ShowSettings(services);
            services.GetRequiredService<HotkeyBinder>().Start();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens the settings window; a second request brings the open one to the front.</summary>
    private void ShowSettings(IServiceProvider services)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        SettingsViewModel model = new(
            services.GetRequiredService<SettingsFile>(), services.GetRequiredService<LiveSettings>(), services.GetRequiredService<ICredentialStore>(),
            services.GetRequiredService<HotkeyBinder>(), [.. services.GetServices<IShareUploader>().Select(uploader => uploader.ProviderName)]);
        _settingsWindow = new SettingsWindow(model);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private static ServiceProvider BuildServices(Func<TopLevel?> owner)
    {
        SettingsFile file = new(SettingsFile.DefaultPath());
        ShareSettings settings = file.Load();
        ServiceCollection services = new();
        services.AddSingleton(file);
        services.AddSingleton(settings);
        services.AddSingleton(new LiveSettings(settings));
        services.AddSingleton(settings.Clipboard);
        services.AddSingleton(settings.Capture);
        services.AddSingleton(settings.Hotkeys);
        services.AddSingleton(settings.Recent);
        services.AddSingleton<BarPositionStore>();
        services.AddSingleton(new RecentLinks(RecentLinks.DefaultPath(), settings.Recent));
        services.AddSingleton<Core.IClipboard>(new AvaloniaClipboard(owner));
        services.AddSingleton<IRegionSelector>(
            EnvironmentRegionSelector.FromEnvironment(Environment.GetEnvironmentVariable(EnvironmentRegionSelector.VariableName)) is { } fixedRegion
                ? new EnvironmentRegionSelector(fixedRegion)
                : new AvaloniaRegionSelector(owner));
        services.AddSingleton<ICaptureHost>(new BarWindowHost(() => owner() as BarWindow));
        AddPlatform(services);
        services.AddS3Storage();
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
