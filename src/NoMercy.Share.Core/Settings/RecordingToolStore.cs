namespace NoMercy.Share.Core.Settings;

/// <summary>The record menu's switch that shows the tool in a recording.</summary>
public sealed class RecordingToolStore(SettingsFile file, LiveSettings live)
{
    public bool Current => live.Current.Recording.ShowTool;

    public event EventHandler? Changed
    {
        add => live.Changed += value;
        remove => live.Changed -= value;
    }

    public void Save(bool show)
    {
        ShareSettings saved = file.Load();
        file.Save(saved with { Recording = saved.Recording with { ShowTool = show } });
        live.Replace(live.Current with { Recording = live.Current.Recording with { ShowTool = show } });
    }
}
