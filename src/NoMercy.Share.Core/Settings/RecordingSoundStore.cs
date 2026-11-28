namespace NoMercy.Share.Core.Settings;

/// <summary>The two sound switches of a recording, shared by the record menu and the Settings page.</summary>
public sealed class RecordingSoundStore(SettingsFile file, LiveSettings live)
{
    public AudioSettings Current => new();

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Save(AudioSettings audio) => _ = (file, live, audio);
}
