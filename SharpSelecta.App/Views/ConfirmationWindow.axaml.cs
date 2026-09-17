using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SharpSelecta.App.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow()
    {
        InitializeComponent();
    }

    public static Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var window = new ConfirmationWindow { Title = title };
        window.MessageText.Text = message;
        return window.ShowDialog<bool>(owner);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
