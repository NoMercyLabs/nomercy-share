namespace NoMercy.Share.Core.Hotkeys;

/// <summary>The main key names a chord may use. Each platform maps these names to its own key codes.</summary>
public static class HotkeyKeys
{
    private static readonly Dictionary<string, string> Names = Build();

    /// <summary>The name in its official spelling ("printscreen" gives "PrintScreen"), or null when it is not a key.</summary>
    public static string? Find(string text) => Names.GetValueOrDefault(text);

    private static Dictionary<string, string> Build()
    {
        List<string> keys =
        [
            "PrintScreen", "Pause", "ScrollLock", "Space", "Tab", "Enter", "Escape",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown", "Left", "Right", "Up", "Down"
        ];
        keys.AddRange(Enumerable.Range(1, 24).Select(number => $"F{number}"));
        keys.AddRange(Enumerable.Range('A', 26).Select(letter => ((char)letter).ToString()));
        keys.AddRange(Enumerable.Range(0, 10).Select(digit => digit.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return keys.ToDictionary(key => key, key => key, StringComparer.OrdinalIgnoreCase);
    }
}
