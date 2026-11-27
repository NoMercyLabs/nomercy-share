using System.Globalization;

namespace NoMercy.Share.Core.Settings;

/// <summary>A largest picture size the user can pick; 0 by 0 is the original size. A size set by hand shows as its numbers.</summary>
public sealed record ResolutionChoice(int Width, int Height)
{
    public string Label => (Width, Height) switch
    {
        (0, 0) => Strings.Get("settings.recording.resolution.original"),
        (3840, 2160) => "4K",
        (1280, 720) or (1920, 1080) or (2560, 1440) => string.Create(CultureInfo.InvariantCulture, $"{Height}p"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Width} × {Height}")
    };
}
