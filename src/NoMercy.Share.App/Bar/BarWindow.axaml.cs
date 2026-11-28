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

    /// <summary>True while the user wants the bar on screen; otherwise it shows only while a notice, a question or a recording needs it.</summary>
    private bool _called;

    private bool _quitting;

    public bool IsClosed { get; private set; }

    /// <summary>The user called the bar: it stays until the cross.</summary>
    public void Summon()
    {
        _called = true;
        Show();
    }

    /// <summary>The cross: the bar goes away and the app, with its hotkeys, stays on.</summary>
    public void Dismiss()
    {
        (DataContext as BarViewModel)?.CloseMenu();
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
        }
        else if (!needed && IsVisible)
        {
            Hide();
        }
    }

    private ICaptureExclusion? _exclusion;

    // Popups and tooltips are windows of their own, so each one is kept out of captures too.
    static BarWindow() => Control.LoadedEvent.AddClassHandler<PopupRoot>((popup, _) => ExcludePopup(popup));

    private static void ExcludePopup(PopupRoot popup)
    {
        TopLevel? owner = popup.ParentTopLevel;
        while (owner is PopupRoot parent)
        {
            owner = parent.ParentTopLevel;
        }

        (owner as BarWindow)?.KeepOutOfCaptures(popup);
    }

    private void KeepOutOfCaptures(TopLevel window)
    {
        nint handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (_exclusion is not null && !_exclusion.Exclude(handle))
        {
            Trace.TraceWarning("A bar window could not be left out of screen captures.");
        }
    }

    public BarWindow()
    {
        InitializeComponent();
        Handle.PointerPressed += OnHandlePressed;
        _elapsedTimer.Tick += (_, _) => (DataContext as BarViewModel)?.RefreshElapsed();
        _saveTimer.Tick += (_, _) => SavePosition();
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            (DataContext as BarViewModel)?.ClearNotice();
        };
    }

    public BarWindow(BarViewModel viewModel, BarPositionStore store, BarSettings settings, ICaptureExclusion? exclusion = null) : this()
    {
        _exclusion = exclusion;
        Opened += (_, _) => KeepOutOfCaptures(this);
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
        Opened += (_, _) => Place();
        PositionChanged += (_, _) =>
        {
            viewModel.CloseRecent();
            if (_placed)
            {
                _saveTimer.Stop();
                _saveTimer.Start();
            }
        };
        Closing += (_, _) => SavePosition();
    }

    /// <summary>Puts the bar at its saved position, clamped to a visible screen, or centered at the top on first open.</summary>
    private void Place()
    {
        BarPosition? target = TargetPosition();
        if (target is not null)
        {
            Position = new PixelPoint((int)Math.Round(target.X), (int)Math.Round(target.Y));
        }

        _placed = true;
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
            _store?.Save(new BarPosition(Position.X, Position.Y));
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
