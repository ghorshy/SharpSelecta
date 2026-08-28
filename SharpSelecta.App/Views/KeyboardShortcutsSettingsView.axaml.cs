using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class KeyboardShortcutsSettingsView : UserControl
{
    // Tracked ourselves rather than trusting e.KeyModifiers on the modifier key's own transition -
    // some backends report that event's modifiers one keystroke stale (e.g. Shift's own KeyDown
    // still reads as Control-only), which froze the live preview at the first-held modifier.
    private KeyModifiers _liveModifiers = KeyModifiers.None;

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

        _liveModifiers = KeyModifiers.None;
        vm.StartRecording(row);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutRowViewModel row } || DataContext is not KeyboardShortcutsViewModel vm)
            return;

        vm.ResetToDefault(row);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutRowViewModel row } || DataContext is not KeyboardShortcutsViewModel vm)
            return;

        vm.ClearShortcut(row);
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

        if (KeyToModifier(e.Key) is { } modifier)
        {
            _liveModifiers |= modifier;
            row.RecordingPreview = FormatModifierPreview(_liveModifiers);
            return;
        }

        vm.CompleteRecording(new KeyGesture(e.Key, _liveModifiers).ToString());
    }

    private void OnRecorderKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not KeyboardShortcutsViewModel vm || vm.RecordingRow is not { } row)
            return;

        e.Handled = true;

        if (KeyToModifier(e.Key) is { } modifier)
        {
            _liveModifiers &= ~modifier;
            row.RecordingPreview = FormatModifierPreview(_liveModifiers);
        }
    }

    private static KeyModifiers? KeyToModifier(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
        Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
        Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
        Key.LWin or Key.RWin => KeyModifiers.Meta,
        _ => null,
    };

    // Reuses KeyGesture's own modifier formatting/ordering so the live preview lines up with
    // the finished gesture text (e.g. "Ctrl+Shift+" growing into "Ctrl+Shift+F") once it completes.
    private static string? FormatModifierPreview(KeyModifiers modifiers) =>
        modifiers == KeyModifiers.None ? null : $"{new KeyGesture(Key.None, modifiers)}+";
}
