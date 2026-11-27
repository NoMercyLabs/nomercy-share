using System.Globalization;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>
/// A region picker that answers from the NOMERCY_SHARE_REGION variable ("x,y,width,height" in virtual screen pixels) instead of
/// showing the overlay, so a scripted run can record without a mouse. The app uses it only while that variable is set.
/// </summary>
public sealed class EnvironmentRegionSelector(ScreenRect region) : IRegionSelector
{
    public const string VariableName = "NOMERCY_SHARE_REGION";

    /// <summary>The region the variable names, or null when it is unset or not four whole numbers.</summary>
    public static ScreenRect? FromEnvironment(string? value)
    {
        string[] parts = (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        int[] numbers = new int[4];
        return parts.Length == numbers.Length
            && parts.Select((part, index) => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[index])).All(parsed => parsed)
                ? new ScreenRect(numbers[0], numbers[1], numbers[2], numbers[3])
                : null;
    }

    public Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default) => Task.FromResult<ScreenRect?>(region);
}
