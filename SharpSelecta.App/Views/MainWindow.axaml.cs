using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
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
