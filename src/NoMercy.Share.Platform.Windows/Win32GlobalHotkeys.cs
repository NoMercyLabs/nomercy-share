using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Hotkeys;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// RegisterHotKey on a hidden message-only window. Create it, register and dispose it on the UI thread:
/// that thread's message loop delivers the presses. No keyboard hook is installed.
/// </summary>
public sealed unsafe class Win32GlobalHotkeys : IGlobalHotkeys
{
    private static readonly nint ClassName = Marshal.StringToHGlobalUni("NoMercyShareHotkeys");
    private static readonly Dictionary<nint, Win32GlobalHotkeys> Owners = [];
    private static bool _classRegistered;

    private readonly nint _window;
    private readonly Dictionary<int, BarAction> _registered = [];
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
        int id = 0;
        foreach ((BarAction action, HotkeyChord chord) in chords)
        {
            id++;
            bool registered = WindowsKeyCodes.TryVirtualKey(chord.Key, out uint key)
                && NativeMethods.RegisterHotKey(_window, id, WindowsKeyCodes.Modifiers(chord.Modifiers) | NativeMethods.ModNoRepeat, key);
            if (registered)
            {
                _registered[id] = action;
            }

            results.Add(new HotkeyRegistration(action, chord, registered));
        }

        return results;
    }

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

    private void Release()
    {
        foreach (int id in _registered.Keys)
        {
            NativeMethods.UnregisterHotKey(_window, id);
        }

        _registered.Clear();
    }

    private void Raise(int id)
    {
        if (!_registered.TryGetValue(id, out BarAction action))
        {
            return;
        }

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
        if (message != NativeMethods.WmHotkey)
        {
            return NativeMethods.DefWindowProc(window, message, wParam, lParam);
        }

        Win32GlobalHotkeys? owner;
        lock (Owners)
        {
            Owners.TryGetValue(window, out owner);
        }

        owner?.Raise((int)wParam);
        return 0;
    }
}
