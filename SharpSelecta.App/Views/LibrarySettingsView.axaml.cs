using Avalonia.Controls;
using Avalonia.Interactivity;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class LibrarySettingsView : UserControl
{
    public LibrarySettingsView()
    {
        InitializeComponent();
    }

    private void OnRemoveFolderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string folderPath } || DataContext is not LibraryViewModel vm)
            return;

        vm.RemoveFolderCommand.Execute(folderPath);
    }
}
