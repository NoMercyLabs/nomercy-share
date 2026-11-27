using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
    private bool _placed;

    public BarWindow()
    {
        InitializeComponent();
        Handle.PointerPressed += OnHandlePressed;
        _saveTimer.Tick += (_, _) => SavePosition();
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            (DataContext as BarViewModel)?.ClearNotice();
        };
    }

    public BarWindow(BarViewModel viewModel, BarPositionStore store, BarSettings settings) : this()
    {
        DataContext = viewModel;
        _store = store;
        _settings = settings;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.PropertyChanged += OnViewModelChanged;
        Opened += (_, _) => Place();
        PositionChanged += (_, _) =>
        {
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
        double scale = RenderScaling;
        List<ScreenArea> areas = [.. Screens.All.Select(s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))];
        double width = Bounds.Width * scale;
        double height = Bounds.Height * scale;
        BarPosition? saved = _store?.Load();
        BarPosition target;
        if (saved is not null)
        {
            target = BarPlacement.Clamp(saved, width, height, areas);
        }
        else if (Screens.Primary is { } primary)
        {
            target = BarPlacement.FirstOpen(new ScreenArea(primary.WorkingArea.X, primary.WorkingArea.Y, primary.WorkingArea.Width, primary.WorkingArea.Height),
                width, _settings.FirstOpenTopMargin * scale);
        }
        else
        {
            _placed = true;
            return;
        }

        Position = new PixelPoint((int)Math.Round(target.X), (int)Math.Round(target.Y));
        _placed = true;
    }

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
        if (e.PropertyName == nameof(BarViewModel.Notice) && DataContext is BarViewModel { Notice: not null })
        {
            _noticeTimer.Stop();
            _noticeTimer.Interval = TimeSpan.FromSeconds(_settings.NoticeSeconds);
            _noticeTimer.Start();
        }
    }
}
