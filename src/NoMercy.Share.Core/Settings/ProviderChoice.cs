namespace NoMercy.Share.Core.Settings;

/// <summary>A storage provider the user can pick: the value settings.json holds, and its name for the window.</summary>
public sealed record ProviderChoice(string Value)
{
    public string Label => Value switch
    {
        "r2" => Strings.Get("settings.provider.r2"),
        "s3" => Strings.Get("settings.provider.s3"),
        _ => Value
    };
}
