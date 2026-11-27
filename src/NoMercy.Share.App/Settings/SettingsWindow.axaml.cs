using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoMercy.Share.Core.Settings;

namespace NoMercy.Share.App;

/// <summary>The settings window. A hotkey field records the chord pressed while it has focus; Win chords come from the global hook in record mode.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as SettingsViewModel)?.ReleaseHotkeys(); // whichever way the window closed, the hotkeys come back
        base.OnClosed(e);
    }

    private void OnChordGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: HotkeyRow row })
        {
            (DataContext as SettingsViewModel)?.HotkeyFieldFocused(row);
        }
    }

    private void OnChordLostFocus(object? sender, RoutedEventArgs e) => (DataContext as SettingsViewModel)?.HotkeyFieldLeft();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => (DataContext as SettingsViewModel)?.Save();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => (DataContext as SettingsViewModel)?.Cancel();

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: HotkeyRow row })
        {
            row.Clear();
        }
    }

    private void OnChordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab || sender is not Control { DataContext: HotkeyRow row })
        {
            return; // Tab keeps moving focus, so the keyboard never gets stuck in the field
        }

        e.Handled = true;
        if (ChordText.From(e.Key, e.KeyModifiers) is { } chord)
        {
            row.Chord = chord;
        }
    }
}
