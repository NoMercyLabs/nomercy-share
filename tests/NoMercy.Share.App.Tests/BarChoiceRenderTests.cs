using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.App.Tests;

public sealed class BarChoiceRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumHitSize = 36;

    [Fact]
    public void AfterAScreenshotTheBarOffersImageLinkAndDone() => HeadlessApp.Run(RenderTheChoice);

    private static void RenderTheChoice()
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-choice-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Stubs stubs = new();
            BarViewModel bar = new(
                new CaptureFlow(stubs, stubs, new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(stubs, stubs, new ExpirySettings(), new ClipboardSettings()));
            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings());

            Task capture = bar.RunAsync(BarAction.FullScreen);
            Dispatcher.UIThread.RunJobs();
            Assert.True(capture.IsCompletedSuccessfully);
            Assert.True(window.IsVisible);

            Button[] buttons = [.. window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("ask") && button.IsEffectivelyVisible)];
            Assert.Equal(["Copy image", "Copy link", "Done"], buttons.Select(button => button.Content));
            Assert.All(buttons, button => Assert.True(button.Bounds.Height >= MinimumHitSize));

            using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
            {
                frame.Save(Path.Combine(folder, "share-choice.png"));
            }

            window.Quit();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class Stubs : IScreenCapture, ICaptureHost, IShareUploader, Core.IClipboard
    {
        private static Task<CaptureResult> Picture() => Task.FromResult(new CaptureResult("shot.png", "image/png"));

        public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken = default) => Picture();

        public Task<CaptureResult> CaptureWindowAsync(CancellationToken cancellationToken = default) => Picture();

        public Task<CaptureResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default) => Picture();

        public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ShowAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string ProviderName => "r2";

        public bool IsConfigured => true;

        public Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShareLink(new Uri("https://share.example.test/abc"), null));

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SetImageAsync(string pngFilePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
