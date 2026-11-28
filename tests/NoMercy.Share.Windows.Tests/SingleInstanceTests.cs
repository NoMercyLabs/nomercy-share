using System.Runtime.Versioning;
using NoMercy.Share.Platform.Windows;

namespace NoMercy.Share.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceTests
{
    private readonly string _name = $"NoMercyShareTest-{Guid.NewGuid():N}";

    [Fact]
    public void OnlyTheFirstCopyOwnsTheName()
    {
        using SingleInstance first = new(_name);
        using SingleInstance second = new(_name);

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
    }

    [Fact]
    public void ALaterCopyCallsTheRunningOne()
    {
        using SingleInstance first = new(_name);
        using ManualResetEventSlim called = new();
        first.Listen(called.Set);

        using (SingleInstance second = new(_name))
        {
            second.CallFirst();
        }

        Assert.True(called.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void TheNameIsFreeAgainAfterTheFirstCopyEnds()
    {
        new SingleInstance(_name).Dispose();

        using SingleInstance next = new(_name);
        Assert.True(next.IsFirst);
    }
}
