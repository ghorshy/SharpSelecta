using Avalonia.Controls;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel { SelectedCategoryViewModel: var category })
        {
            category.CancelCommand.Execute(null);
        }
    }
}
