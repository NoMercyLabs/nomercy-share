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
