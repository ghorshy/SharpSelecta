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

    // OK applies every category with edits, not just the one on screen; closing any other way drops them
    // all, including a theme or accent still being previewed.
    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel vm)
            return;

        foreach (var category in vm.Categories.Where(c => c.HasPendingChanges))
        {
            if (category.ApplyCommand is IAsyncRelayCommand applyAsync)
                await applyAsync.ExecuteAsync(null);
            else
                category.ApplyCommand.Execute(null);
        }

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel vm)
            return;

        foreach (var category in vm.Categories)
        {
            category.CancelCommand.Execute(null);
        }
    }
}
