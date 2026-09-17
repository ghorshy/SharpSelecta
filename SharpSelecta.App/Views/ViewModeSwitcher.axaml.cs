using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public sealed partial class ViewModeSwitcher : UserControl
{
    public static readonly StyledProperty<LibraryViewMode> SelectedModeProperty =
        AvaloniaProperty.Register<ViewModeSwitcher, LibraryViewMode>(
            nameof(SelectedMode), defaultBindingMode: BindingMode.TwoWay);

    public LibraryViewMode SelectedMode
    {
        get => GetValue(SelectedModeProperty);
        set => SetValue(SelectedModeProperty, value);
    }

    public ViewModeSwitcher()
    {
        InitializeComponent();
    }

    private void OnListClicked(object? sender, RoutedEventArgs e) => SelectedMode = LibraryViewMode.TrackList;

    private void OnCoverArtClicked(object? sender, RoutedEventArgs e) => SelectedMode = LibraryViewMode.AlbumGrid;
}
