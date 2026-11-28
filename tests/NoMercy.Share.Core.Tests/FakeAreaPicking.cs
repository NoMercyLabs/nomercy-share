using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.Core.Tests;

/// <summary>A picker overlay that stays open until the test chooses, presses Escape or the token cancels it.</summary>
internal sealed class FakeRegionSelector : IRegionSelector
{
    private readonly SemaphoreSlim _opened = new(0);
    private TaskCompletionSource<ScreenRect?>? _open;

    public int Opens { get; private set; }

    public async Task<ScreenRect?> SelectAsync(ScreenFrame frozen, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<ScreenRect?> open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _open = open;
        Opens++;
        await using CancellationTokenRegistration registration = cancellationToken.Register(() => open.TrySetResult(null));
        _opened.Release();
        return await open.Task;
    }

    public async Task WaitUntilOpenAsync() => Assert.True(await _opened.WaitAsync(TimeSpan.FromSeconds(10)), "no picker opened");

    public void Choose() => _open!.TrySetResult(new ScreenRect(0, 0, 10, 10));

    public void Escape() => _open!.TrySetResult(null);

    public static async Task PickAsync(IRegionSelector picker, CancellationToken cancellationToken)
    {
        ScreenFrame frozen = null!; // the fake never looks at the picture
        if (await picker.SelectAsync(frozen, cancellationToken) is null)
        {
            throw new OperationCanceledException();
        }
    }
}
