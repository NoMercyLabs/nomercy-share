namespace NoMercy.Share.Core.Capture;

/// <summary>Finds which encoder families work on this PC for a codec, by a real test encode. The CPU is always in the answer.</summary>
public interface IEncoderProbe
{
    Task<IReadOnlyList<string>> WorkingFamiliesAsync(string codec, CancellationToken cancellationToken = default);
}
