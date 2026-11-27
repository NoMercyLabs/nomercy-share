using Avalonia.Markup.Xaml;
using NoMercy.Share.Core;

namespace NoMercy.Share.App;

/// <summary>Reads a text from Strings.resx in XAML: <c>ToolTip.Tip="{local:Loc bar.region}"</c>.</summary>
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(key);
}
