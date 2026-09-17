using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SharpSelecta.App.Views;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public static async Task<string?> ShowAsync(Window owner, string title, string? initialValue)
    {
        var window = new TextPromptWindow { Title = title };
        window.ValueBox.Text = initialValue;
        var result = await window.ShowDialog<string?>(owner);
        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(ValueBox.Text);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnValueBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close(ValueBox.Text);
        }
    }
}
