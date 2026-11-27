using Avalonia;
using Avalonia.Media;

namespace NoMercy.Share.App.Icons;

/// <summary>
/// A Moooom icon by its Moooom name, the same name <c>MoooomIcon</c> takes on web and Android. The path data is
/// generated from the designer's SVG files (<see cref="MoooomIconData"/>), drawn stroked at 1.5 on a 24-unit box
/// like the SVG itself. An unknown name draws nothing, so the word beside it still carries the meaning.
/// </summary>
public sealed class MoooomIcon : Avalonia.Controls.Shapes.Path
{
    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<MoooomIcon, string>(nameof(Kind), "");

    static MoooomIcon() => KindProperty.Changed.AddClassHandler<MoooomIcon>((icon, _) => icon.Data = Shape(icon.Kind));

    public MoooomIcon()
    {
        // Stretch.Uniform would scale every path to its own bounds (a gear and a cross would fill the box differently),
        // so the path keeps its 24-unit coordinates and the whole box is scaled by size / 24.
        Stretch = Stretch.None;
        RenderTransformOrigin = RelativePoint.TopLeft;
        StrokeThickness = 1.5;
        StrokeLineCap = PenLineCap.Round;
        StrokeJoin = PenLineJoin.Round;
    }

    /// <summary>The side of the square view box the icon paths are drawn in.</summary>
    private const double ViewBox = 24;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && Bounds.Width > 0)
        {
            double scale = Bounds.Width / ViewBox;
            RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    /// <summary>Every icon name this build can draw.</summary>
    public static IReadOnlyCollection<string> Names => MoooomIconData.Paths.Keys.ToList();

    /// <summary>Parsed when drawn, so reading <see cref="Names"/> needs no render platform.</summary>
    private static Geometry? Shape(string kind) =>
        MoooomIconData.Paths.TryGetValue(kind, out string? data) ? Geometry.Parse(data) : null;

    /// <summary>The Moooom name of the icon.</summary>
    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
}
