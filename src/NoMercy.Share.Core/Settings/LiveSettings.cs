namespace NoMercy.Share.Core.Settings;

/// <summary>The settings in use right now. The settings window replaces them on Save; each share and recording reads them when it starts.</summary>
public sealed class LiveSettings(ShareSettings initial)
{
    private volatile ShareSettings _current = initial;

    public ShareSettings Current => _current;

    public void Replace(ShareSettings next) => _current = next;
}
