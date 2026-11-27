namespace NoMercy.Share.Core.Settings;

/// <summary>A value settings.json holds and its name for the window, looked up as "{group}.{value}" in the strings.</summary>
public sealed record OptionChoice(string Group, string Value)
{
    public string Label => Strings.Get($"{Group}.{Value}");
}
