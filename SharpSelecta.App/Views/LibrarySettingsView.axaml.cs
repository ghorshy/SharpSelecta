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
        if (!RowClickHelper.TryGetRowContext<string, LibraryViewModel>(sender, DataContext, out var folderPath, out var vm))
            return;

        vm.RemovePendingFolderCommand.Execute(folderPath);
    }
}
