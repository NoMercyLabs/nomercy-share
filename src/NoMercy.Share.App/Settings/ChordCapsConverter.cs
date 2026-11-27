using System.Globalization;
using Avalonia.Data.Converters;

namespace NoMercy.Share.App;

/// <summary>Splits the chord text ("Win+Shift+S") into the key names the hotkey field draws as key caps.</summary>
public sealed class ChordCapsConverter : IValueConverter
{
    public static readonly ChordCapsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string[] caps = value is string { Length: > 0 } chord ? chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
        return caps;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
