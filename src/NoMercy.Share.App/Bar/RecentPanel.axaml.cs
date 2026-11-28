using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoMercy.Share.Core.Bar;
using NoMercy.Share.Core.Recent;

namespace NoMercy.Share.App;

/// <summary>The list of recent links under the bar. Arrow keys move, Enter copies, Escape closes.</summary>
public partial class RecentPanel : UserControl
{
    public RecentPanel()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        AttachedToVisualTree += (_, _) => FocusFirstRow();
    }

    private BarViewModel? Bar => DataContext as BarViewModel;

    private void FocusFirstRow()
    {
        if (Bar is { IsRecentEmpty: false })
        {
            List.SelectedIndex = 0;
            List.Focus();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            Bar?.CloseRecent();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && List.SelectedItem is RecentRow row)
        {
            Bar?.CopyRecentCommand.Execute(row);
            e.Handled = true;
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e) => Bar?.CopyRecentCommand.Execute((sender as Control)?.DataContext);

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Bar?.OpenRecentCommand.Execute((sender as Control)?.DataContext);

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => Bar?.DeleteRecentCommand.Execute((sender as Control)?.DataContext);
}
