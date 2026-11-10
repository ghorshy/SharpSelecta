using Avalonia.Controls;
using Avalonia.Interactivity;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class CompilationFlagsWindow : Window
{
    public CompilationFlagsWindow()
    {
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner, CompilationFlagsViewModel viewModel)
    {
        var window = new CompilationFlagsWindow { DataContext = viewModel };
        _ = viewModel.LoadAsync();
        return window.ShowDialog(owner);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
