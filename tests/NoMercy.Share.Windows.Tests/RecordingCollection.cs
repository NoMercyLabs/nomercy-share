namespace NoMercy.Share.Windows.Tests;

/// <summary>Tests that record the screen or sound, or run a heavy encode, run one at a time: a parallel encode starves a recording of CPU and its sound.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RecordingCollection
{
    public const string Name = "Recording";
}
