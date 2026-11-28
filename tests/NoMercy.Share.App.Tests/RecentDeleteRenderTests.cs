using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    public void EveryRecentRowOffersCopyOpenAndDelete() => HeadlessApp.Run(() => WithRecent(deletes: true, (_, panel, host, _) =>
    {
        Button[][] rows = [.. panel.GetVisualDescendants().OfType<ListBoxItem>()
            .Select(VisibleButtons)];

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
    public void NoRowMovesOrResizesWhileOneAsksDeletesAndFails() => HeadlessApp.Run(() => WithRecent(deletes: true, (_, panel, host, uploader) =>
    {
        uploader.DeleteWaits = new TaskCompletionSource();
        Rect[] normal = Layout(panel);

        Click(panel, row: 1, "Delete");
        AssertRowShows(panel, 1, "inline-asking.png", host, ["Delete", "Cancel"], "Delete from the host?", "Cached up to 1 hour.");
        Assert.Equal(normal, Layout(panel));

        Click(panel, row: 1, "Delete");
        AssertRowShows(panel, 1, "inline-deleting.png", host, [], "Deleting...");
        Assert.Equal(normal, Layout(panel));

        uploader.DeleteWaits.SetException(new ShareUploadException("the host answered 403 Forbidden because this made-up key may not delete objects"));
        Dispatcher.UIThread.RunJobs();
        AssertRowShows(panel, 1, "inline-failed.png", host, ["Try again", "Cancel"], "Not deleted");
        Assert.Equal(normal, Layout(panel));
        TextBlock reason = VisibleTexts(Row(panel, 1)).Single(text => text.Text!.StartsWith("the host answered", StringComparison.Ordinal));
        Assert.Equal(reason.Text, ToolTip.GetTip(reason));
        Log(normal);

        Click(panel, row: 1, "Cancel");
        Assert.Equal(["Copy link", "Open in browser", "Delete"], VisibleButtons(Row(panel, 1)).Select(Name));
        Assert.Equal(normal, Layout(panel));
    }));

    [Fact]
    public void AskingToRemoveAListEntryKeepsTheLayout() => HeadlessApp.Run(() => WithRecent(deletes: false, (_, panel, host, _) =>
    {
        Rect[] normal = Layout(panel);

        Click(panel, row: 0, "Delete");

        AssertRowShows(panel, 0, "inline-list-only.png", host, ["Remove", "Cancel"], "Only remove from the list?", "The upload stays online.");
        Assert.Equal(normal, Layout(panel));
    }));

    [Fact]
    public void TheQuestionFocusesCancelAndTabReachesBothAnswersAndEscapeTakesItBack() => HeadlessApp.Run(() => WithRecent(deletes: true, (bar, panel, host, _) =>
    {
        Click(panel, row: 0, "Delete");

        Assert.Equal("Cancel", Name(Focused(host)));
        Assert.False(ShowsRing(Row(panel, 0)), "the row and the focused answer both show a ring");
        Press(host, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("Delete", Name(Focused(host)));
        Press(host, PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("Cancel", Name(Focused(host)));
        Save(host, "inline-focus.png");

        Press(host, PhysicalKey.Escape, RawInputModifiers.None);

        Assert.True(ShowsRing(Row(panel, 0)), "a focused row shows its ring");
        Assert.True(bar.IsRecentOpen);
        Assert.Equal(["Copy link", "Open in browser", "Delete"], VisibleButtons(Row(panel, 0)).Select(Name));
        Press(host, PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(bar.IsRecentOpen);
    }));

    [Fact]
    public void ADeleteKeepsTheListWhereItWasScrolled() => HeadlessApp.Run(() => WithRecent(deletes: false, (bar, panel, host, _) =>
    {
        ListBox list = panel.GetVisualDescendants().OfType<ListBox>().Single();
        ScrollViewer scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        list.ScrollIntoView(9);
        Dispatcher.UIThread.RunJobs();
        Vector before = scroll.Offset;
        Assert.True(before.Y > 0, "the list did not scroll");
        Control row = list.ContainerFromIndex(8)!;
        RecentRow deleted = (RecentRow)row.DataContext!;

        ClickIn(row, "Delete");
        ClickIn(row, "Remove");

        Assert.DoesNotContain(deleted, bar.Recent.Rows);
        Assert.Equal(before, scroll.Offset);
        Assert.Same(bar.Recent.Rows[8], list.SelectedItem);
        Save(host, "delete-keeps-scroll.png");
    }, moreLinks: 9));

    private static void ClickIn(Control row, string button)
    {
        VisibleButtons(row).Single(candidate => Name(candidate) == button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static bool ShowsRing(ListBoxItem row) =>
        row.GetVisualDescendants().OfType<ContentPresenter>().First(presenter => presenter.Name == "PART_ContentPresenter" && presenter.TemplatedParent == row)
            .BorderBrush is ISolidColorBrush { Color.A: > 0 };

    private static ListBoxItem Row(RecentPanel panel, int index) => panel.GetVisualDescendants().OfType<ListBoxItem>().ElementAt(index);

    private static Button[] VisibleButtons(Control within) => [.. within.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible)];

    private static TextBlock[] VisibleTexts(Control within) =>
        [.. within.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))];

    private static string? Name(Button button) => AutomationProperties.GetName(button) ?? button.Content as string;

    private static Button Focused(Window window) => Assert.IsType<Button>(window.FocusManager.GetFocusedElement());

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(RecentPanel panel, int row, string button)
    {
        VisibleButtons(Row(panel, row)).Single(candidate => Name(candidate) == button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The panel's size, then every row's place and size inside it.</summary>
    private static Rect[] Layout(RecentPanel panel) =>
    [
        new Rect(panel.Bounds.Size),
        .. panel.GetVisualDescendants().OfType<ListBoxItem>().Select(row => new Rect(row.TranslatePoint(default, panel)!.Value, row.Bounds.Size))
    ];

    private static void AssertRowShows(RecentPanel panel, int index, string picture, Window host, string[] buttons, params string[] texts)
    {
        Save(host, picture);
        ListBoxItem row = Row(panel, index);
        Button[] shown = VisibleButtons(row);
        Assert.Equal(buttons, shown.Select(Name));
        Assert.All(shown, button =>
        {
            Assert.True(button.Bounds.Height >= MinimumRowButtonSize);
            Assert.True(button.TranslatePoint(new Point(button.Bounds.Width, button.Bounds.Height), row)!.Value is var end && end.X <= row.Bounds.Width && end.Y <= row.Bounds.Height);
        });
        TextBlock[] visible = VisibleTexts(row);
        foreach (string expected in texts)
        {
            TextBlock text = visible.Single(block => block.Text == expected);
            Assert.False(text.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{expected} is cut off");
            Assert.True(text.Bounds.Height >= text.TextLayout.Height, $"{expected} is clipped");
            Assert.True(text.TranslatePoint(new Point(0, text.Bounds.Height), row)!.Value.Y <= row.Bounds.Height, $"{expected} leaves the row");
        }
    }

    private static void Log(Rect[] layout)
    {
        if (Environment.GetEnvironmentVariable(RenderFolderVariable) is { Length: > 0 } folder)
        {
            File.WriteAllLines(Path.Combine(folder, "inline-measurements.txt"), layout.Select(rect => rect.ToString()));
        }
    }

    private static void WithRecent(bool deletes, Action<BarViewModel, RecentPanel, Window, FakeUploader> test, int moreLinks = 0)
    {
        string root = Path.Combine(Path.GetTempPath(), $"share-delete-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            RecentLinks recent = new(Path.Combine(root, "recent.json"), new RecentSettings(), () => Now);
            recent.Add(new RecentLink("https://share.example.test/exp-30d/Qx7LmP2aRt.png", RecentKind.Image, "image/png", 48_000, Now.AddMinutes(-5), Now.AddDays(29), null));
            recent.Add(new RecentLink("https://share.example.test/exp-7d/Zk3VbN8cYd.mp4", RecentKind.Video, "video/mp4", 9_000_000, Now.AddHours(-3), Now.AddDays(4), null));
            recent.Add(new RecentLink("https://share.example.test/exp-30d/Hh5TgW1eUo.png", RecentKind.Image, "image/png", 52_000, Now.AddDays(-31), Now.AddDays(-1), null));
            for (int older = 0; older < moreLinks; older++)
            {
                recent.Add(new RecentLink($"https://share.example.test/exp-30d/older-{older}.png", RecentKind.Image, "image/png", 40_000, Now.AddDays(-2), Now.AddDays(27), null));
            }

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

            test(bar, panel, host, uploader);
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
