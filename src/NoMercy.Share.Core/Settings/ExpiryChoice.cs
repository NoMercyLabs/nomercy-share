namespace NoMercy.Share.Core.Settings;

/// <summary>One lifetime the user can pick as the default: a number of days, or null for never. The label is made here, at the display edge.</summary>
public sealed record ExpiryChoice(int? Days)
{
    public string Label => Days switch
    {
        null => Strings.Get("settings.expiry.never"),
        1 => Strings.Get("settings.expiry.day", 1),
        { } days => Strings.Get("settings.expiry.days", days)
    };
}
