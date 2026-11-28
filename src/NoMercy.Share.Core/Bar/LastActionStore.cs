using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Bar;

/// <summary>Remembers what the user started last, in the settings file.</summary>
public sealed class LastActionStore(SettingsFile file)
{
    public LastAction Load() => file.Load().Bar.LastAction;

    public void Save(LastAction action)
    {
        ShareSettings settings = file.Load();
        file.Save(settings with { Bar = settings.Bar with { LastAction = action } });
    }
}
