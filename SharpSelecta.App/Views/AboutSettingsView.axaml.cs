using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class AboutSettingsView : UserControl
{
    public AboutSettingsView()
    {
        InitializeComponent();
    }

    private async void OnCopyVersionInfoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AboutViewModel about || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        await clipboard.SetTextAsync(about.VersionInfo);
        about.IsVersionInfoCopied = true;
    }
}
