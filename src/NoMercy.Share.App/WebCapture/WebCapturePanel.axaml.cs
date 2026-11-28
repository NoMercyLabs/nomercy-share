using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>The web page options joined under the bar. Enter captures; the bar window handles Escape.</summary>
public partial class WebCapturePanel : UserControl
{
    public WebCapturePanel()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnEnter, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => AddressBox.Focus());
    }

    /// <summary>A choice row would take Enter for itself, so the panel takes it first and captures.</summary>
    private void OnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnCaptureClick(sender, e);
        }
    }

    private void OnCaptureClick(object? sender, RoutedEventArgs e) => _ = (DataContext as WebCaptureViewModel)?.CaptureAsync();
}
