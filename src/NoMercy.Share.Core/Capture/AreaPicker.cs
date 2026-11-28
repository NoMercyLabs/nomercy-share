namespace NoMercy.Share.Core.Capture;

/// <summary>The area picker every capture goes through, so an open one can be canceled when the user chooses something else.</summary>
public sealed class AreaPicker(IRegionSelector overlay) : IRegionSelector
{
    public Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default) =>
        overlay.SelectAsync(frozen, cancellationToken);
}
