using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Headless;

namespace NoMercy.Share.App.Tests;

/// <summary>Avalonia allows one setup per process and ties it to one thread, so every headless test shares this one: the app's own styles, real drawing, one UI thread.</summary>
internal static class HeadlessApp
{
    private static readonly BlockingCollection<Action> Work = [];
    private static readonly Thread UiThread = Start();

    /// <summary>Makes sure the app is set up.</summary>
    public static void Ensure() => Run(() => { });

    /// <summary>Runs the action on the UI thread and waits; an exception in it reaches the caller.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;
        using ManualResetEventSlim done = new();
        Work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();
        if (failure is not null)
        {
            throw new InvalidOperationException("A headless UI test failed.", failure);
        }
    }

    private static Thread Start()
    {
        Thread thread = new(() =>
        {
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            foreach (Action action in Work.GetConsumingEnumerable())
            {
                action();
            }
        })
        {
            IsBackground = true,
            Name = "headless-ui"
        };
        thread.Start();
        return thread;
    }
}
