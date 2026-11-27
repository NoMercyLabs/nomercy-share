using System.Diagnostics;
using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>Opens an https address in the default browser through the shell. Any other scheme is refused, so a link cannot start a program.</summary>
public sealed class ShellUrlOpener(Action<ProcessStartInfo>? start = null) : IUrlOpener
{
    private readonly Action<ProcessStartInfo> _start = start ?? (info => Process.Start(info)?.Dispose());

    public void Open(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(Strings.Get("error.url.https"), nameof(url));
        }

        _start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
