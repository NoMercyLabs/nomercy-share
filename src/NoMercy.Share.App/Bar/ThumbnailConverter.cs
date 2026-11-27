using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace NoMercy.Share.App;

/// <summary>Turns the path of a local picture into a small bitmap for a list row; null when the file cannot be read.</summary>
public sealed class ThumbnailConverter : IValueConverter
{
    /// <summary>Wide enough for the 40-unit tile at 2x scaling.</summary>
    private const int DecodeWidth = 80;

    public static readonly ThumbnailConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path)
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
