using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoMercy.Share.Core;

/// <summary>Change notification for the view models; the views bind to it.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stores the value and raises <see cref="PropertyChanged"/> only when it changed.</summary>
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnChanged(name);
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for a property that is computed from others.</summary>
    protected void OnChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
