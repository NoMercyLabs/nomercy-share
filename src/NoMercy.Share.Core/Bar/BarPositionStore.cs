using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Bar;

/// <summary>Remembers where the user left the bar, in the settings file.</summary>
public sealed class BarPositionStore(SettingsFile file)
{
    public BarPosition? Load() => file.Load().Bar.Position;

    public void Save(BarPosition position)
    {
        ShareSettings settings = file.Load();
        file.Save(settings with { Bar = settings.Bar with { Position = position } });
    }
}
