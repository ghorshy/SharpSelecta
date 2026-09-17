using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public sealed partial class ViewModeSwitcher : UserControl
{
    public static readonly StyledProperty<LibraryViewMode> SelectedModeProperty =
        AvaloniaProperty.Register<ViewModeSwitcher, LibraryViewMode>(
            nameof(SelectedMode), defaultBindingMode: BindingMode.TwoWay);

    static ViewModeSwitcher()
    {
        SelectedModeProperty.Changed.AddClassHandler<ViewModeSwitcher>((s, _) => s.UpdateLabel());
    }

    public LibraryViewMode SelectedMode
    {
        get => GetValue(SelectedModeProperty);
        set => SetValue(SelectedModeProperty, value);
    }

    public ViewModeSwitcher()
    {
        InitializeComponent();
        UpdateLabel();
    }

    private void UpdateLabel() =>
        LabelText.Text = SelectedMode == LibraryViewMode.AlbumGrid ? Strings.ViewModeCoverArt : Strings.ViewModeList;

    private void OnListClicked(object? sender, RoutedEventArgs e) => SelectedMode = LibraryViewMode.TrackList;

    private void OnCoverArtClicked(object? sender, RoutedEventArgs e) => SelectedMode = LibraryViewMode.AlbumGrid;
}
