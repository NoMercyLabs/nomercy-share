using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App;

/// <summary>The floating bar window: borderless, always on top, moved by its handle, and back where the user left it.</summary>
public partial class BarWindow : Window
{
    /// <summary>A drag fires many position changes; the position is written once this long after the last one.</summary>
    private static readonly TimeSpan PositionSaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly BarPositionStore? _store;
    private readonly BarSettings _settings = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = PositionSaveDelay };
    private readonly DispatcherTimer _noticeTimer = new();
    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _placed;

    /// <summary>Where the user put the bar. A wider row can push the window left of it for as long as it is wide.</summary>
    private PixelPoint _home;

    private PixelPoint? _fitted;

    /// <summary>True while the user wants the bar on screen; otherwise it shows only while a notice, a question or a recording needs it.</summary>
    private bool _called;

    private bool _quitting;

    public bool IsClosed { get; private set; }

    /// <summary>The user called the bar: it stays until the cross.</summary>
    public void Summon()
    {
        _called = true;
        Show();
        MoveToPointerScreen();
    }

    /// <summary>The cross: the bar goes away and the app, with its hotkeys, stays on.</summary>
    public void Dismiss()
    {
        // An open web panel would otherwise stay open on a hidden bar and swallow the next call.
        (DataContext as BarViewModel)?.WebPanel?.Cancel();
        (DataContext as BarViewModel)?.ReturnToLastMode();
        _called = false;
        ShowWhileNeeded();
    }

    public void Quit()
    {
        _quitting = true;
        Close();
    }

    /// <summary>Hides the bar before a picture; true when it was on screen, so the capture puts it back.</summary>
    public bool HideForCapture()
    {
        bool shown = IsVisible;
        Hide();
        return shown;
    }

    /// <summary>Puts a wanted bar back on top of an open area picker, so another choice can be clicked. A hidden bar stays hidden.</summary>
    public void StayAbovePicker()
    {
        if (!IsNeeded)
        {
            return;
        }

        Show();
        // Setting Topmost again moves the window above the other topmost windows without taking the focus.
        Topmost = false;
        Topmost = true;
    }

    private bool IsNeeded =>
        _called || DataContext is BarViewModel bar && (bar.Notice is not null || bar.Ask is not null || bar.Choice is not null || bar.IsRecording);

    private void ShowWhileNeeded()
    {
        bool needed = IsNeeded;
        if (needed && !IsVisible)
        {
            Show();
            MoveToPointerScreen();
        }
        else if (!needed && IsVisible)
        {
            Hide();
        }
    }

    private ICaptureExclusion? _exclusion;
    private IPointerMonitor? _pointer;

    private readonly List<TopLevel> _popups = [];

    // Popups and tooltips are windows of their own, so each one follows the bar in and out of captures.
    static BarWindow() => Control.LoadedEvent.AddClassHandler<PopupRoot>((popup, _) => FollowPopup(popup));

    private static void FollowPopup(PopupRoot popup)
    {
        TopLevel? owner = popup.ParentTopLevel;
        while (owner is PopupRoot parent)
        {
            owner = parent.ParentTopLevel;
        }

        if (owner is BarWindow bar)
        {
            bar._popups.Add(popup);
            popup.Unloaded += (_, _) => bar._popups.Remove(popup);
            bar.ApplyCaptureExclusion(popup);
        }
    }

    private void ApplyCaptureExclusion(TopLevel window)
    {
        if (_exclusion is null)
        {
            return;
        }

        bool excluded = (DataContext as BarViewModel)?.IsBarExcludedFromCaptures ?? true;
        nint handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (!(excluded ? _exclusion.Exclude(handle) : _exclusion.Include(handle)))
        {
            Trace.TraceWarning(excluded ? "A bar window could not be left out of screen captures." : "A bar window could not be shown in screen captures.");
        }
    }

    public BarWindow()
    {
        InitializeComponent();
        Handle.PointerPressed += OnHandlePressed;
        // The links are a window of their own, so their width cannot bind to the row.
        Pill.SizeChanged += (_, e) => Links.Card.Width = e.NewSize.Width;
        _elapsedTimer.Tick += (_, _) => (DataContext as BarViewModel)?.RefreshElapsed();
        _saveTimer.Tick += (_, _) => SavePosition();
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            (DataContext as BarViewModel)?.ClearNotice();
        };
    }

    public BarWindow(
        BarViewModel viewModel, BarPositionStore store, BarSettings settings, ICaptureExclusion? exclusion = null, IPointerMonitor? pointer = null) : this()
    {
        _exclusion = exclusion;
        _pointer = pointer;
        Opened += (_, _) => ApplyCaptureExclusion(this);
        DataContext = viewModel;
        _store = store;
        _settings = settings;
        viewModel.CloseRequested += (_, _) => Dismiss();
        viewModel.ShowRequested += (_, _) => Summon();
        Closing += (_, e) =>
        {
            // Alt+F4 or a close from code only hides the bar; Quit, sign-out and app shutdown really close it.
            if (!_quitting && e.CloseReason is WindowCloseReason.WindowClosing or WindowCloseReason.Undefined)
            {
                e.Cancel = true;
                Dismiss();
            }
        };
        Closed += (_, _) => IsClosed = true;
        viewModel.PropertyChanged += OnViewModelChanged;
        Opened += (_, _) =>
        {
            Place();
            MoveToPointerScreen();
        };
        PositionChanged += (_, e) =>
        {
            if (e.Point == _fitted)
            {
                return;
            }

            _fitted = null;
            _home = e.Point;
            viewModel.CloseRecent();
            if (_placed)
            {
                _saveTimer.Stop();
                _saveTimer.Start();
            }
        };
        SizeChanged += (_, _) => FitToScreen();
        Closing += (_, _) => SavePosition();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && DataContext is BarViewModel bar)
        {
            bar.Escape();
            e.Handled = true;
        }
    }

    /// <summary>Puts the bar at its saved position, clamped to a visible screen, or centered at the top on first open.</summary>
    private void Place()
    {
        BarPosition? target = TargetPosition();
        if (target is not null)
        {
            Position = new PixelPoint((int)Math.Round(target.X), (int)Math.Round(target.Y));
        }

        _home = Position;
        _placed = true;
    }

    /// <summary>Keeps a bar that grew inside the screen its row is on, and puts it back once it fits there again.</summary>
    private void FitToScreen()
    {
        if (!_placed)
        {
            return;
        }

        double width = Bounds.Width * RenderScaling;
        double height = Bounds.Height * RenderScaling;
        // A point in the row: under an open panel the middle of the window can lie on another screen, or on none.
        int inRow = (int)((Pill.Margin.Top + Pill.Bounds.Height) * RenderScaling);
        Screen? screen = Screens.ScreenFromPoint(_home + new PixelPoint(inRow, inRow));
        ScreenArea[] areas = screen is null ? [.. Screens.All.Select(ToArea)] : [ToArea(screen)];
        BarPosition inside = BarPlacement.Clamp(new BarPosition(_home.X, _home.Y), width, height, areas);
        PixelPoint fitted = new((int)Math.Floor(inside.X), (int)Math.Floor(inside.Y));
        if (fitted != Position)
        {
            _fitted = fitted;
            Position = fitted;
        }
    }

    /// <summary>Brings the bar to the screen the mouse is on, at the same place as on the screen it was on.</summary>
    private void MoveToPointerScreen()
    {
        double width = Bounds.Width * RenderScaling;
        double height = Bounds.Height * RenderScaling;
        PixelPoint center = new(Position.X + (int)(width / 2), Position.Y + (int)(height / 2));
        if (!_placed || PointerScreen.Of(Screens, _pointer) is not { } to || Screens.ScreenFromPoint(center) is not { } from || from.Bounds == to.Bounds)
        {
            return;
        }

        BarPosition moved = BarPlacement.MoveTo(new BarPosition(Position.X, Position.Y), width, height, ToArea(from), ToArea(to));
        Position = new PixelPoint((int)Math.Round(moved.X), (int)Math.Round(moved.Y));
    }

    private BarPosition? TargetPosition()
    {
        double scale = RenderScaling;
        double width = Bounds.Width * scale;
        double height = Bounds.Height * scale;
        if (_store?.Load() is { } saved)
        {
            return BarPlacement.Clamp(saved, width, height, [.. Screens.All.Select(ToArea)]);
        }

        return Screens.Primary is { } primary
            ? BarPlacement.FirstOpen(ToArea(primary), width, _settings.FirstOpenTopMargin * scale)
            : null;
    }

    private static ScreenArea ToArea(Screen screen) =>
        new(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height);

    private void SavePosition()
    {
        _saveTimer.Stop();
        if (_placed)
        {
            _store?.Save(new BarPosition(_home.X, _home.Y));
        }
    }

    private void OnHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BarViewModel.IsBarExcludedFromCaptures))
        {
            foreach (TopLevel window in (TopLevel[])[this, .. _popups])
            {
                ApplyCaptureExclusion(window);
            }
        }

        // The bar does not take the focus when shown, and the panel's address field is typed in.
        if (e.PropertyName == nameof(BarViewModel.WebPanel) && DataContext is BarViewModel { WebPanel: not null })
        {
            Activate();
        }

        if (e.PropertyName == nameof(BarViewModel.IsRecording) && DataContext is BarViewModel bar)
        {
            _elapsedTimer.IsEnabled = bar.IsRecording;
        }

        if (e.PropertyName == nameof(BarViewModel.Notice) && DataContext is BarViewModel { Notice: not null })
        {
            _noticeTimer.Stop();
            _noticeTimer.Interval = TimeSpan.FromSeconds(_settings.NoticeSeconds);
            _noticeTimer.Start();
        }

        if (e.PropertyName is nameof(BarViewModel.Notice) or nameof(BarViewModel.Ask) or nameof(BarViewModel.Choice) or nameof(BarViewModel.IsRecording))
        {
            ShowWhileNeeded();
        }
    }
}
