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

        vm.Folders.RemovePendingFolderCommand.Execute(folderPath);
    }

    private async void OnFindInconsistentCompilationFlagsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm && TopLevel.GetTopLevel(this) is Window owner)
        {
            await CompilationFlagsWindow.ShowAsync(owner, vm.CreateCompilationFlags());
        }
    }
}
