using System.Runtime.Versioning;

namespace NoMercy.Share.Platform.Windows;

/// <summary>One running copy per user. A later copy calls the first one (which shows its bar) and ends.</summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _owner;
    private readonly EventWaitHandle _call;
    private RegisteredWaitHandle? _listening;

    /// <summary>Create and dispose it on the same thread: the first copy holds the mutex for that thread.</summary>
    public SingleInstance(string name)
    {
        // "Local\" keeps both names inside this user's sign-in session.
        _owner = new Mutex(initiallyOwned: true, $@"Local\{name}", out bool createdNew);
        IsFirst = createdNew;
        _call = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.call");
    }

    public bool IsFirst { get; }

    /// <summary>Runs on a pool thread each time a later copy calls.</summary>
    public void Listen(Action called) =>
        _listening = ThreadPool.RegisterWaitForSingleObject(_call, (_, _) => called(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void CallFirst() => _call.Set();

    public void Dispose()
    {
        _listening?.Unregister(null);
        if (IsFirst)
        {
            _owner.ReleaseMutex();
        }

        _owner.Dispose();
        _call.Dispose();
    }
}
