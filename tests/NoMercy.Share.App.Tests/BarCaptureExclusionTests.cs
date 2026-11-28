using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

/// <summary>What the windows ask of the system while the tool is shown in a recording.</summary>
public sealed class BarCaptureExclusionTests
{
    [Fact]
    public void TheBarIsLetIntoARecordingAndTakenOutAgainWhileTheBorderNeverIs() => HeadlessApp.Run(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-exclusion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            FakeScreenRecorder recorder = new();
            SettingsFile file = new(Path.Combine(root, "settings.json"));
            BarViewModel bar = new(
                new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(new FakeUploader(), new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
                recorder: recorder,
                tool: new RecordingToolStore(file, new LiveSettings(new ShareSettings())));
            Exclusion barCalls = new();
            Exclusion borderCalls = new();
            BarWindow window = new(bar, new BarPositionStore(file), new BarSettings(), barCalls);
            _ = new RecordingBorderPresenter(recorder, () => [(new ScreenRect(0, 0, 1920, 1080), 1.0)], borderCalls, new PassThrough());
            window.Summon();
            bar.ToggleToolInRecordingCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["exclude"], barCalls.Calls.Distinct());

            Pump(bar.RunAsync(BarAction.Record));
            Pump(bar.StartRecordingAsync());
            Assert.Equal("include", barCalls.Calls[^1]);

            Pump(bar.RunAsync(BarAction.Record));
            Assert.Equal("exclude", barCalls.Calls[^1]);
            Assert.NotEmpty(borderCalls.Calls);
            Assert.DoesNotContain("include", borderCalls.Calls);
            window.Quit();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    });

    private static void Pump(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully, "the UI work did not finish");
    }

    private sealed class Exclusion : ICaptureExclusion
    {
        public List<string> Calls { get; } = [];

        public bool Exclude(nint window) => Add("exclude");

        public bool Include(nint window) => Add("include");

        private bool Add(string call)
        {
            Calls.Add(call);
            return true;
        }
    }

    private sealed class PassThrough : IInputPassThrough
    {
        public bool Apply(nint window) => true;
    }
}
