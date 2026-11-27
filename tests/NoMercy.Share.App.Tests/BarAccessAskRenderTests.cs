using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App.Tests;

public sealed class BarAccessAskRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumHitSize = 36;

    [Fact]
    public void TheAskShowsTwoReachableButtonsAndRenders() => HeadlessApp.Run(RenderTheAsk);

    private static void RenderTheAsk()
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "file"), "x");
            CaptureFiles files = new(new CaptureSettings { Folder = Path.Combine(root, "file", "Captures") }, "unused", () => DateTimeOffset.UnixEpoch, Path.Combine(root, "fallback"));
            // The ask never runs a capture or a share, so the flows stay unset.
            BarViewModel bar = new(null!, null!, files: files, folderAccess: new AlwaysOn());
            bar.CheckFolderAccess();

            BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(root, "position.json"))), new BarSettings());
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button[] buttons = [.. window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("ask"))];
            Assert.Equal(2, buttons.Length);
            Assert.All(buttons, button =>
            {
                Assert.True(button.Focusable);
                Assert.True(button.Bounds.Height >= MinimumHitSize, $"{button.Content} is {button.Bounds.Height} high");
                Assert.True(button.Bounds.Width >= MinimumHitSize, $"{button.Content} is {button.Bounds.Width} wide");
            });

            using Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
            {
                frame.Save(Path.Combine(folder, "share-cfa-ask.png"));
            }

            window.Close();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class AlwaysOn : IFolderAccess
    {
        public bool IsProtectionOn() => true;

        public Task<bool> RequestAccessAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
