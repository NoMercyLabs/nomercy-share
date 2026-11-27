using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>
/// The region picker for one monitor: the frozen picture of that monitor, dimmed outside the selection, an accent edge and a size label.
/// Drag selects, mouse-up or Enter confirms, Esc cancels.
/// </summary>
public partial class RegionOverlayWindow : Window
{
    private readonly ScreenRect _monitor;
    private readonly double _scale = 1;
    private Point? _anchor;
    private Point _current;

    public RegionOverlayWindow()
    {
        InitializeComponent();
    }

    /// <param name="monitorFrame">The frozen picture of this monitor, placed on the virtual screen.</param>
    /// <param name="scale">The monitor's display scale: physical pixels per device-independent unit.</param>
    public RegionOverlayWindow(ScreenFrame monitorFrame, double scale) : this()
    {
        _monitor = monitorFrame.Bounds;
        _scale = scale;
        Frozen.Source = FrameBitmap.From(monitorFrame);
        Width = monitorFrame.Width / scale;
        Height = monitorFrame.Height / scale;
        Position = new PixelPoint(monitorFrame.X, monitorFrame.Y);
        Dim.Data = DimGeometry(null);
        Opened += (_, _) => Activate();
    }

    /// <summary>The user dragged a selection and confirmed it; the rectangle is in virtual screen pixels.</summary>
    public event EventHandler<ScreenRect>? Confirmed;

    /// <summary>The user pressed Esc.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Draws a selection between two points (device-independent units, relative to this window).</summary>
    public void SetSelection(Point from, Point to)
    {
        _anchor = from;
        _current = to;
        Redraw();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _anchor = e.GetPosition(this);
            _current = _anchor.Value;
            e.Pointer.Capture(this);
            Redraw();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_anchor is not null)
        {
            _current = e.GetPosition(this);
            Redraw();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_anchor is null)
        {
            return;
        }

        e.Pointer.Capture(null);
        _current = e.GetPosition(this);
        Confirm();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm();
        }
    }

    /// <summary>A click with no drag selects nothing, so the picker waits for a real drag.</summary>
    private void Confirm()
    {
        ScreenRect selection = Selection();
        if (selection.IsEmpty)
        {
            _anchor = null;
            Redraw();
            return;
        }

        Confirmed?.Invoke(this, selection);
    }

    private ScreenRect Selection() => _anchor is { } anchor
        ? RegionMath.FromDrag(anchor.X, anchor.Y, _current.X, _current.Y, _scale, _monitor.X, _monitor.Y, _monitor)
        : default;

    private void Redraw()
    {
        ScreenRect selection = Selection();
        bool visible = !selection.IsEmpty;
        Edge.IsVisible = visible;
        SizeLabel.IsVisible = visible;
        Dim.Data = DimGeometry(visible ? selection : null);
        if (!visible)
        {
            return;
        }

        double edge = Resource("Nm-Space-0-5");
        Rect area = new((selection.X - _monitor.X) / _scale, (selection.Y - _monitor.Y) / _scale, selection.Width / _scale, selection.Height / _scale);
        Canvas.SetLeft(Edge, area.X - edge);
        Canvas.SetTop(Edge, area.Y - edge);
        Edge.Width = area.Width + (2 * edge);
        Edge.Height = area.Height + (2 * edge);
        PlaceSizeLabel(selection);
    }

    private void PlaceSizeLabel(ScreenRect selection)
    {
        SizeText.Text = Strings.Get("overlay.size", selection.Width, selection.Height);
        SizeLabel.Measure(Size.Infinity);
        Size label = SizeLabel.DesiredSize;
        ScreenRect local = new(selection.X - _monitor.X, selection.Y - _monitor.Y, selection.Width, selection.Height);
        (double x, double y) = RegionMath.LabelPosition(local, label.Width * _scale, label.Height * _scale, Resource("Nm-Space-2") * _scale, _monitor.Width, _monitor.Height);
        Canvas.SetLeft(SizeLabel, x / _scale);
        Canvas.SetTop(SizeLabel, y / _scale);
    }

    /// <summary>The whole window with the selection cut out (even-odd fill), so only the outside is dimmed.</summary>
    private Geometry DimGeometry(ScreenRect? selection)
    {
        GeometryGroup group = new() { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, _monitor.Width / _scale, _monitor.Height / _scale)));
        if (selection is { } cut)
        {
            group.Children.Add(new RectangleGeometry(new Rect((cut.X - _monitor.X) / _scale, (cut.Y - _monitor.Y) / _scale, cut.Width / _scale, cut.Height / _scale)));
        }

        return group;
    }

    private double Resource(string key) => this.TryFindResource(key, ActualThemeVariant, out object? value) && value is double number
        ? number
        : throw new InvalidOperationException($"The theme has no number named {key}.");
}
