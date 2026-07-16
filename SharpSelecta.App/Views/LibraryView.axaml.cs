using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: LibraryTrackViewModel item })
        {
            item.Library.PlayNowCommand.Execute(item.Track);
        }
    }
}
