using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoMercy.Share.Core.Capture;

namespace NoMercy.Share.App;

/// <summary>The "Capture a web page" window. Enter captures and Escape cancels.</summary>
public partial class WebCaptureWindow : Window
{
    public WebCaptureWindow()
    {
        InitializeComponent();
    }

    public WebCaptureWindow(WebCaptureViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        Opened += (_, _) => AddressBox.Focus();
        Closed += (_, _) => viewModel.StopRunning();
        Surface.PointerPressed += OnSurfacePressed;
        AddHandler(KeyDownEvent, OnChoiceEnter, RoutingStrategies.Tunnel);
    }

    /// <summary>A choice row would take Enter for itself, so the window takes it first and captures.</summary>
    private void OnChoiceEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.Source is ListBoxItem)
        {
            e.Handled = true;
            OnCaptureClick(sender, e);
        }
    }

    /// <summary>The window has no OS frame, so its background moves it; a control keeps its own press.</summary>
    private void OnSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is WebCaptureViewModel model)
        {
            e.Handled = true;
            model.Cancel();
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCaptureClick(object? sender, RoutedEventArgs e) => _ = (DataContext as WebCaptureViewModel)?.CaptureAsync();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => (DataContext as WebCaptureViewModel)?.Cancel();
}
