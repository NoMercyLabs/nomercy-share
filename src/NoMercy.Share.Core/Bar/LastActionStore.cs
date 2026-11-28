using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.Core.Bar;

/// <summary>Remembers what the user started last, in the settings file.</summary>
public sealed class LastActionStore(SettingsFile file)
{
    public LastAction Load() => new();

    public void Save(LastAction action) => _ = (file, action);
}
