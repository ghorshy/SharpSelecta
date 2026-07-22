using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel { SelectedCategoryViewModel: var category })
            return;

        if (category.ApplyCommand is IAsyncRelayCommand applyAsync)
            await applyAsync.ExecuteAsync(null);
        else
            category.ApplyCommand.Execute(null);

        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel { SelectedCategoryViewModel: var category })
        {
            category.CancelCommand.Execute(null);
        }
    }
}
