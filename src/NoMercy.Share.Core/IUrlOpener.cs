namespace NoMercy.Share.Core;

/// <summary>Opens an address in the user's browser. The platform implements it and refuses anything but https.</summary>
public interface IUrlOpener
{
    /// <summary>Throws <see cref="ArgumentException"/> for an address that is not an absolute https one.</summary>
    void Open(Uri url);
}
