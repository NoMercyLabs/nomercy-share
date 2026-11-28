namespace NoMercy.Share.Core.Settings;

/// <summary>The two sound switches of a recording, shared by the record menu and the Settings page.</summary>
public sealed class RecordingSoundStore(SettingsFile file, LiveSettings live)
{
    public AudioSettings Current => live.Current.Recording.Audio;

    public event EventHandler? Changed
    {
        add => live.Changed += value;
        remove => live.Changed -= value;
    }

    public void Save(AudioSettings audio)
    {
        ShareSettings saved = file.Load();
        file.Save(saved with { Recording = saved.Recording with { Audio = audio } });
        live.Replace(live.Current with { Recording = live.Current.Recording with { Audio = audio } });
    }
}
