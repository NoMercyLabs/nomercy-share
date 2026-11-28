using Avalonia;
using NoMercy.Share.Core;
using NoMercy.Share.Core.NativeHost;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.App;

/// <summary>NoMercy Share: the floating bar.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(Strings.Get("unsupported.os"));
            return 2;
        }

        if (NativeHostArguments.Origin(args) is { } origin)
        {
            return NativeHostMain.Run(origin);
        }

        // One copy per user: a second start shows the running copy's bar and ends.
        using SingleInstance instance = new(ShareDefaults.SingleInstanceName);
        if (!instance.IsFirst)
        {
            instance.CallFirst();
            return 0;
        }

        bool hidden = args.Contains(ShareDefaults.BackgroundArgument, StringComparer.OrdinalIgnoreCase);
        Action<Action> listen = instance.Listen;
        AppBuilder.Configure(() => new App(hidden, listen)).UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // The previewers call this by name, so it stays public.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
