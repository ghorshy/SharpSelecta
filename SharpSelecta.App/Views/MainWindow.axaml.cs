using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        RebuildKeyBindings(vm);
        vm.ShortcutSettings.ShortcutsChanged += (_, _) => RebuildKeyBindings(vm);
    }

    private void RebuildKeyBindings(MainWindowViewModel vm)
    {
        KeyBindings.Clear();
        foreach (var shortcut in ShortcutRegistry.All)
        {
            try
            {
                var gesture = KeyGesture.Parse(vm.ShortcutSettings.GetEffectiveGesture(shortcut.Id));
                KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = shortcut.Command(vm) });
            }
            catch (ArgumentException)
            {
                // Corrupted/hand-edited settings file - skip this shortcut rather than crash.
            }
        }
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
