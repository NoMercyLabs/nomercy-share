using Avalonia;
using NoMercy.Share.Core;
using NoMercy.Share.Core.NativeHost;

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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // The previewers call this by name, so it stays public.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
