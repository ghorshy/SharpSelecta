using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class AlbumArtSearchWindow : Window
{
    public AlbumArtSearchWindow()
    {
        InitializeComponent();
    }

    // The chosen cover, or null if the user cancelled. The searches start as the window opens and are
    // abandoned if it closes first.
    public static async Task<byte[]?> ShowAsync(Window owner, AlbumArtSearchViewModel viewModel)
    {
        var window = new AlbumArtSearchWindow { DataContext = viewModel };
        viewModel.CloseRequested += (_, result) => window.Close(result);
        _ = viewModel.SearchAsync();
        try
        {
            return await window.ShowDialog<byte[]?>(owner);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    private void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AlbumArtTileViewModel tile } && DataContext is AlbumArtSearchViewModel viewModel)
        {
            viewModel.Select(tile);
        }
    }

    private async void OnEnterKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: AlbumArtTileViewModel tile } || DataContext is not AlbumArtSearchViewModel viewModel)
            return;

        var key = await TextPromptWindow.ShowAsync(this, Strings.AlbumArtKeyPromptTitle, Strings.AlbumArtKeyPrompt, initialValue: null);
        if (key is not null)
        {
            await viewModel.ConfigureAsync(tile, key);
        }
    }
}
