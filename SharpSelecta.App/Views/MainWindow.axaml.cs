using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e) =>
        (DataContext as MainWindowViewModel)?.PersistRightColumnWidth();
}
