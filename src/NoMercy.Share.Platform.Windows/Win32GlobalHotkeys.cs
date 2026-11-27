using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// RegisterHotKey on a hidden message-only window. Create it, register and dispose it on the UI thread:
/// that thread's message loop delivers the presses. A Win chord the shell refuses is served by a keyboard hook instead.
/// </summary>
public sealed unsafe class Win32GlobalHotkeys : IGlobalHotkeys
{
    private static readonly nint ClassName = Marshal.StringToHGlobalUni("NoMercyShareHotkeys");
    private static readonly Dictionary<nint, Win32GlobalHotkeys> Owners = [];
    private static bool _classRegistered;

    private readonly nint _window;
    private readonly Dictionary<int, BarAction> _registered = [];
    private KeyboardHook? _hook;
    private bool _disposed;

    public Win32GlobalHotkeys()
    {
        nint instance = NativeMethods.GetModuleHandle(0);
        EnsureWindowClass(instance);
        _window = NativeMethods.CreateWindow(0, ClassName, 0, 0, 0, 0, 0, 0, NativeMethods.MessageOnlyParent, 0, instance, 0);
        if (_window == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        lock (Owners)
        {
            Owners[_window] = this;
        }
    }

    public event EventHandler<BarAction>? Pressed;

    public IReadOnlyList<HotkeyRegistration> Register(IReadOnlyDictionary<BarAction, HotkeyChord> chords)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Release();
        List<HotkeyRegistration> results = [];
        Dictionary<BarAction, HotkeyChord> hooked = [];
        int id = 0;
        foreach ((BarAction action, HotkeyChord chord) in chords)
        {
            id++;
            bool known = WindowsKeyCodes.TryVirtualKey(chord.Key, out uint key);
            bool registered = known
                && NativeMethods.RegisterHotKey(_window, id, WindowsKeyCodes.Modifiers(chord.Modifiers) | NativeMethods.ModNoRepeat, key);
            if (registered)
            {
                _registered[id] = action;
            }
            else if (known && chord.Modifiers.HasFlag(HotkeyModifiers.Win))
            {
                hooked[action] = chord; // the shell owns most Win chords, so the keyboard hook serves them instead
            }

            results.Add(new HotkeyRegistration(action, chord, registered));
        }

        return hooked.Count == 0 ? results : [.. results.Select(result => hooked.ContainsKey(result.Action) ? result with { Registered = InstallHook(hooked) } : result)];
    }

    /// <summary>True while the keyboard hook is installed, which happens only when a chord needs it.</summary>
    public bool HookInstalled => _hook is not null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release();
        lock (Owners)
        {
            Owners.Remove(_window);
        }

        NativeMethods.DestroyWindow(_window);
    }

    private bool InstallHook(Dictionary<BarAction, HotkeyChord> hooked)
    {
        _hook ??= KeyboardHook.TryInstall(new HookChordMatcher(hooked), _window);
        return _hook is not null;
    }

    private void Release()
    {
        _hook?.Dispose();
        _hook = null;
        foreach (int id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_window, id);
        }

        _registered.Clear();
    }

    private void Raise(int id)
    {
        if (_registered.TryGetValue(id, out BarAction action))
        {
            Raise(action);
        }
    }

    private void RaiseHooked(BarAction action)
    {
        if (_hook is not null)
        {
            Raise(action);
        }
    }

    private void Raise(BarAction action)
    {
        try
        {
            Pressed?.Invoke(this, action);
        }
        catch (Exception error)
        {
            Trace.TraceError("Hotkey handler for {0} failed: {1}", action, error); // an exception must not cross back into Windows
        }
    }

    private static void EnsureWindowClass(nint instance)
    {
        lock (Owners)
        {
            if (_classRegistered)
            {
                return;
            }

            NativeMethods.WindowClass windowClass = new()
            {
                Size = (uint)sizeof(NativeMethods.WindowClass),
                WindowProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WindowProc,
                Instance = instance,
                ClassName = ClassName
            };
            if (NativeMethods.RegisterClass(in windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _classRegistered = true;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProc(nint window, uint message, nint wParam, nint lParam)
    {
        if (message is not (NativeMethods.WmHotkey or NativeMethods.WmHookedChord))
        {
            return NativeMethods.DefWindowProc(window, message, wParam, lParam);
        }

        Win32GlobalHotkeys? owner;
        lock (Owners)
        {
            Owners.TryGetValue(window, out owner);
        }

        if (message == NativeMethods.WmHotkey)
        {
            owner?.Raise((int)wParam);
        }
        else
        {
            owner?.RaiseHooked((BarAction)(int)wParam);
        }

        return 0;
    }
}
