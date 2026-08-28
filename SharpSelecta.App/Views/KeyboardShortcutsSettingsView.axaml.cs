using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class KeyboardShortcutsSettingsView : UserControl
{
    private static readonly HashSet<Key> ModifierKeys =
    [
        Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift,
        Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin,
    ];

    public KeyboardShortcutsSettingsView()
    {
        InitializeComponent();

        // Tunnel + handledEventsToo so recording reliably wins even if the pressed combo
        // is already bound elsewhere - see avalonia_pointerwheel_tunnel_bubble_gotcha.
        AddHandler(KeyDownEvent, OnRecorderKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnRecorderKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutRowViewModel row } || DataContext is not KeyboardShortcutsViewModel vm)
            return;

        vm.StartRecording(row);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutRowViewModel row } || DataContext is not KeyboardShortcutsViewModel vm)
            return;

        vm.ResetToDefault(row);
    }

    private void OnRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not KeyboardShortcutsViewModel vm || vm.RecordingRow is not { } row)
            return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            vm.CancelRecording();
            return;
        }

        if (ModifierKeys.Contains(e.Key))
        {
            row.RecordingPreview = FormatModifierPreview(e.KeyModifiers);
            return;
        }

        vm.CompleteRecording(new KeyGesture(e.Key, e.KeyModifiers).ToString());
    }

    private void OnRecorderKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not KeyboardShortcutsViewModel vm || vm.RecordingRow is not { } row)
            return;

        e.Handled = true;
        row.RecordingPreview = FormatModifierPreview(e.KeyModifiers);
    }

    // Reuses KeyGesture's own modifier formatting/ordering so the live preview lines up with
    // the finished gesture text (e.g. "Ctrl+Shift+" growing into "Ctrl+Shift+F") once it completes.
    private static string? FormatModifierPreview(KeyModifiers modifiers) =>
        modifiers == KeyModifiers.None ? null : $"{new KeyGesture(Key.None, modifiers)}+";
}
