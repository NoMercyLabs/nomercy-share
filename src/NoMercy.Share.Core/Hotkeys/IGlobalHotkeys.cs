using NoMercy.Share.Core.Bar;

namespace NoMercy.Share.Core.Hotkeys;

/// <summary>Keys that work while another app has focus. Each platform registers its own implementation.</summary>
public interface IGlobalHotkeys : IDisposable
{
    /// <summary>Raised on the UI thread when the chord of an action is pressed.</summary>
    event EventHandler<BarAction>? Pressed;

    /// <summary>Registers the chords, replacing any from an earlier call. A chord that cannot be had is reported, never thrown.</summary>
    IReadOnlyList<HotkeyRegistration> Register(IReadOnlyDictionary<BarAction, HotkeyChord> chords);

    /// <summary>Turns the hotkeys off until the handle is disposed: nothing is raised and every key goes on. Pauses nest; Register during one applies at the resume.</summary>
    IDisposable Pause();
}
