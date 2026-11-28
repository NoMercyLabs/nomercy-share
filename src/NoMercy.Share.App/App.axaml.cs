using System.Diagnostics;
using System.Security;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Hotkeys;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Platform.Windows;
using NoMercy.Share.Storage.Discord;
using NoMercy.Share.Storage.S3;

namespace NoMercy.Share.App;

/// <summary>The composition root: picks the platform module, registers the providers and opens the bar.</summary>
public sealed class App : Application
{
    private readonly bool _startHidden;
    private readonly Action<Action>? _onCalled;
    private BarWindow? _window;
    private SettingsWindow? _settingsWindow;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    // The previewers build the app with no arguments.
    public App()
    {
    }

    /// <param name="startHidden">The sign-in start: the app runs with its hotkeys and no bar.</param>
    /// <param name="onCalled">Hands over the callback that a second start of the app runs; it shows the bar.</param>
    public App(bool startHidden, Action<Action> onCalled)
    {
        _startHidden = startHidden;
        _onCalled = onCalled;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            ServiceProvider services = BuildServices(() => _window);
            ShareSettings settings = services.GetRequiredService<ShareSettings>();
            BarWindow window = new(services.GetRequiredService<BarViewModel>(), services.GetRequiredService<BarPositionStore>(), settings.Bar);
            _window = window;
            // The bar is not the main window: hiding it keeps the app on, and only Quit ends it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            services.GetRequiredService<BarViewModel>().SettingsRequested += (_, _) => ShowSettings(services);
            services.GetRequiredService<HotkeyBinder>().Start();
            services.GetRequiredService<BarViewModel>().CheckFolderAccess();
            RegisterNativeHost(settings.NativeHost);
            ApplySignInStart(settings.Bar);
            _onCalled?.Invoke(() => Dispatcher.UIThread.Post(window.Summon));
            if (!_startHidden)
            {
                window.Summon();
            }

            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Quit()
    {
        _window?.Quit();
        _desktop?.Shutdown();
    }

    /// <summary>Adds or removes the sign-in start. A failure here never stops the bar.</summary>
    private static void ApplySignInStart(BarSettings settings)
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is not { } exe)
        {
            return;
        }

        try
        {
            new SignInStart(exe).Apply(settings.StartAtSignIn);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceError("The sign-in start could not be set: {0}", error.Message);
        }
    }

    /// <summary>Tells Edge and Chrome where this exe is. A failure here never stops the bar.</summary>
    private static void RegisterNativeHost(NativeHostSettings settings)
    {
        if (!OperatingSystem.IsWindows() || !settings.Register || Environment.ProcessPath is not { } exe)
        {
            return;
        }

        try
        {
            new NativeHostRegistration(Path.GetDirectoryName(SettingsFile.DefaultPath())!, exe, settings).Register();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceError("The native messaging host could not be registered: {0}", error.Message);
        }
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
            services.GetRequiredService<HotkeyBinder>(), [.. services.GetServices<IShareUploader>().Select(uploader => uploader.ProviderName)],
            services.GetService<IEncoderProbe>());
        model.QuitRequested += (_, _) => Quit();
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
        services.AddSingleton<IRegionSelector>(sp =>
            EnvironmentRegionSelector.FromEnvironment(Environment.GetEnvironmentVariable(EnvironmentRegionSelector.VariableName)) is { } fixedRegion
                ? new EnvironmentRegionSelector(fixedRegion)
                : new AvaloniaRegionSelector(owner, sp.GetService<IForegroundGrant>() ?? new NoForegroundGrant()));
        services.AddSingleton<IWebCaptureDialog>(sp => new WebCaptureDialog(
            sp.GetRequiredService<Core.IClipboard>(), () => sp.GetRequiredService<LiveSettings>().Current.WebCapture));
        services.AddSingleton<ICaptureHost>(new BarWindowHost(() => owner() as BarWindow));
        AddPlatform(services);
        services.AddS3Storage();
        services.AddDiscordStorage();
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
