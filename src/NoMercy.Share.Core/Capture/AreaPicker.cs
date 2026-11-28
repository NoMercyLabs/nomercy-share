namespace NoMercy.Share.Core.Capture;

/// <summary>The area picker every capture goes through, so an open one can be canceled when the user chooses something else.</summary>
public sealed class AreaPicker(IRegionSelector overlay) : IRegionSelector
{
    private CancellationTokenSource? _open;

    public async Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource open = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _open = open;
        try
        {
            return await overlay.SelectAsync(frozen, open.Token);
        }
        finally
        {
            _open = null;
        }
    }

    /// <summary>Cancels the open picker; false when none is open.</summary>
    public bool Cancel()
    {
        if (_open is not { IsCancellationRequested: false } open)
        {
            return false;
        }

        open.Cancel();
        return true;
    }
}
