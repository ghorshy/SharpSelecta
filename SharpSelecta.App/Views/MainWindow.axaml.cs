using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class MainWindow : Window
{
    private ShortcutBindings? _shortcuts;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        // Tunnel so a wheel shortcut preempts the ScrollViewer under the pointer - see avalonia_pointerwheel_tunnel_bubble_gotcha.
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        RebuildKeyBindings(vm);
        vm.ShortcutSettings.ShortcutsChanged += (_, _) => RebuildKeyBindings(vm);
        vm.OpenPropertiesRequested += async (_, _) => await OpenPropertiesForFocusedTrackAsync(vm);
    }

    private async Task OpenPropertiesForFocusedTrackAsync(MainWindowViewModel vm)
    {
        if (FocusedTracks(vm) is { Count: > 0 } tracks)
        {
            await TrackPropertiesWindow.ShowAsync(this, vm.Library, tracks);
        }
    }

    // The selected track(s) of whichever list currently has focus; nothing for a text box or tile.
    private IReadOnlyList<Track>? FocusedTracks(MainWindowViewModel vm)
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused)
            return null;

        if (focused.FindAncestorOfType<DataGrid>(includeSelf: true) is { SelectedItem: LibraryTrackViewModel { IsMissing: false } gridItem })
            return vm.Library.ResolveSelection(gridItem.Track);

        return focused.FindAncestorOfType<ListBox>(includeSelf: true)?.SelectedItem switch
        {
            AlbumTrackRowViewModel row => [row.Track.Track],
            QueueEntryViewModel entry => [entry.Entry.Track],
            _ => null,
        };
    }

    private void RebuildKeyBindings(MainWindowViewModel vm)
    {
        _shortcuts = new ShortcutBindings(vm, () => ScopesOf(FocusManager?.GetFocusedElement() as Visual));
        KeyBindings.Clear();
        foreach (var (gesture, command) in _shortcuts.Keys)
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = command });
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_shortcuts is not null && WheelGesture.FromWheel(e.KeyModifiers, e.Delta.Y) is { } gesture &&
            _shortcuts.TryHandleWheel(gesture, ScopesOf(e.Source as Visual), Math.Abs(e.Delta.Y)))
            e.Handled = true;
    }

    private static ShortcutScope ScopesOf(Visual? element)
    {
        var scopes = ShortcutScope.Global;
        for (var visual = element; visual is not null; visual = visual.GetVisualParent())
        {
            scopes |= visual switch
            {
                AlbumGridView => ShortcutScope.AlbumGrid,
                TrackListView => ShortcutScope.TrackList,
                QueueView => ShortcutScope.Queue,
                _ => ShortcutScope.Global,
            };
        }

        return scopes;
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e) =>
        (DataContext as MainWindowViewModel)?.PersistRightColumnWidth();

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var isArrowKeyNavigationFocused = e.NewFocusedElement is TextBox ||
            (e.NewFocusedElement as Visual)?.FindAncestorOfType<DataGrid>(includeSelf: true) is not null;

        vm.PlaybackControls.IsArrowKeyNavigationFocused = isArrowKeyNavigationFocused;
    }
}
