using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
        if (Bar is { Recent.IsEmpty: false })
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
            TakeBackQuestionOrClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && List.SelectedItem is RecentRow { State.IsNormal: true } row)
        {
            Bar?.CopyRecentCommand.Execute(row);
            e.Handled = true;
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e) => Bar?.CopyRecentCommand.Execute((sender as Control)?.DataContext);

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Bar?.OpenRecentCommand.Execute((sender as Control)?.DataContext);

    private void TakeBackQuestionOrClose()
    {
        if (Bar?.Recent.ResetQuestions() != true)
        {
            Bar?.CloseRecent();
        }
        else if (List.SelectedItem is RecentRow row)
        {
            FocusRow(row);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Bar is { } bar && (sender as Control)?.DataContext is RecentRow row)
        {
            bar.Recent.AskToDelete(row);
            if (FocusRow(row) is { } container)
            {
                container.UpdateLayout();
                container.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("cancel")).Focus(NavigationMethod.Tab);
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is RecentRow row)
        {
            Bar?.Recent.ResetQuestions();
            FocusRow(row);
        }
    }

    private async void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (Bar is { } bar && (sender as Control)?.DataContext is RecentRow row)
        {
            Task delete = bar.Recent.DeleteAsync(row);
            FocusRow(row); // the pressed button is gone, and without focus in the panel Escape would not reach it
            await delete;
            if (bar.IsRecentOpen && !IsKeyboardFocusWithin)
            {
                FocusFirstRow();
            }
        }
    }

    /// <summary>Selects the row and gives it the keyboard; returns its container, or null when the list has none for it.</summary>
    private Control? FocusRow(RecentRow row)
    {
        List.SelectedItem = row;
        Control? container = List.ContainerFromItem(row);
        container?.Focus();
        return container;
    }
}
