using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Capture;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;
using NoMercy.Share.Core.Tests;

namespace NoMercy.Share.App.Tests;

public sealed class RecentDeleteRenderTests
{
    private const string RenderFolderVariable = "SHARE_RENDER_DIR";
    private const double MinimumRowButtonSize = 28;
    private const int PictureScale = 2;

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EveryRecentRowOffersCopyOpenAndDelete() => HeadlessApp.Run(() => WithRecent(deletes: true, (_, panel, host) =>
    {
        Button[][] rows = [.. panel.GetVisualDescendants().OfType<ListBoxItem>()
            .Select(row => row.GetVisualDescendants().OfType<Button>().ToArray())];

        Assert.Equal(3, rows.Length);
        Assert.All(rows, buttons =>
        {
            Assert.Equal(["Copy link", "Open in browser", "Delete"], buttons.Select(AutomationProperties.GetName));
            Assert.All(buttons, button => Assert.True(button.Bounds.Width >= MinimumRowButtonSize && button.Bounds.Height >= MinimumRowButtonSize));
            Assert.Single(buttons.Select(button => button.Bounds.Y).Distinct());
        });
        Assert.Single(rows.Select(buttons => buttons[^1].TranslatePoint(default, panel)!.Value.X).Distinct());
        Save(host, "delete-recent-list.png");
        TextBlock[] paths = [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("row-path"))];
        Assert.All(paths, path => Assert.False(path.TextLayout.TextLines[0].HasCollapsed, $"{path.Text} is cut off"));
    }));

    [Fact]
    public void DeletingAnUploadTheHostCanRemoveAsksBeforeDeleting() => HeadlessApp.Run(() =>
        AskFromTheFirstRow(deletes: true, "ask.delete.host", "delete-confirm-online.png"));

    [Fact]
    public void DeletingAnUploadTheHostCannotRemoveAsksToRemoveItFromTheList() => HeadlessApp.Run(() =>
        AskFromTheFirstRow(deletes: false, "ask.delete.list", "delete-confirm-list-only.png"));

    private static void AskFromTheFirstRow(bool deletes, string question, string picture) => WithRecent(deletes, (bar, panel, _) =>
    {
        BarWindow window = new(bar, new BarPositionStore(new SettingsFile(Path.Combine(Path.GetTempPath(), $"share-delete-{Guid.NewGuid():N}", "position.json"))), new BarSettings());
        window.Summon();
        Button delete = panel.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetName(button) == "Delete");

        delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Border ask = window.FindControl<Border>("AccessAsk")!;
        Assert.True(ask.IsEffectivelyVisible);
        Assert.Equal(Strings.Get(question), bar.Ask!.Text);
        Button[] answers = [.. ask.GetVisualDescendants().OfType<Button>()];
        Assert.Equal(["Delete", "Cancel"], answers.Select(button => button.Content));
        Assert.All(answers, button => Assert.True(button.Bounds.Right <= ask.Bounds.Width && button.Bounds.Height >= MinimumRowButtonSize));
        TextBlock text = ask.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == bar.Ask.Text);
        Assert.True(text.Bounds.Height >= text.TextLayout.Height, "the question is cut off");
        Save(window, picture);
        window.Quit();
    });

    private static void WithRecent(bool deletes, Action<BarViewModel, RecentPanel, Window> test)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-delete-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            RecentLinks recent = new(Path.Combine(root, "recent.json"), new RecentSettings(), () => Now);
            recent.Add(new RecentLink("https://share.example.test/exp-30d/Qx7LmP2aRt.png", RecentKind.Image, "image/png", 48_000, Now.AddMinutes(-5), Now.AddDays(29), null));
            recent.Add(new RecentLink("https://share.example.test/exp-7d/Zk3VbN8cYd.mp4", RecentKind.Video, "video/mp4", 9_000_000, Now.AddHours(-3), Now.AddDays(4), null));
            recent.Add(new RecentLink("https://share.example.test/exp-30d/Hh5TgW1eUo.png", RecentKind.Image, "image/png", 52_000, Now.AddDays(-31), Now.AddDays(-1), null));
            FakeUploader uploader = new() { Deletes = deletes };
            BarViewModel bar = new(
                new CaptureFlow(new FakeScreenCapture(), new FakeCaptureHost(), new CaptureSettings { HideDelayMilliseconds = 0 }),
                new ShareFlow(uploader, new FakeClipboard(), new ExpirySettings(), new ClipboardSettings()),
                clock: () => Now,
                recent: recent,
                opener: new FakeUrlOpener(),
                clipboard: new FakeClipboard(),
                deletion: new ShareDeletion(() => [uploader]));
            Assert.True(bar.RunAsync(BarAction.Recent).IsCompletedSuccessfully);
            RecentPanel panel = new() { DataContext = bar };
            Window host = new() { Content = panel, SizeToContent = SizeToContent.WidthAndHeight };
            host.Show();
            Dispatcher.UIThread.RunJobs();

            test(bar, panel, host);
            host.Close();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Save(Window window, string picture)
    {
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        window.SetRenderScaling(PictureScale);
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(folder, picture));
    }
}
