using Avalonia.Controls;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

// Checked only when every track carries the flag; clicking flips that for all of them.
internal static class CompilationFlagMenu
{
    private const string ItemClass = "compilation-flag";

    public static async void Refresh(ContextMenu menu, LibraryViewModel library, IReadOnlyList<Track> tracks)
    {
        if (menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Classes.Contains(ItemClass)) is not { } item)
            return;

        item.IsEnabled = false;
        item.IsChecked = false;
        item.Tag = await library.AreAllCompilationsAsync(tracks);
        item.IsChecked = item.Tag is true;
        item.IsEnabled = true;
    }

    public static Task ToggleAsync(MenuItem item, LibraryViewModel library, IReadOnlyList<Track> tracks) =>
        library.SetCompilationFlagAsync(tracks, item.Tag is not true);
}
