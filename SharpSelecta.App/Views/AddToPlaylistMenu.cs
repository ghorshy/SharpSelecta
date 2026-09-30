using System;
using Avalonia.Controls;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

// Shared by every "Add to playlist" submenu. XAML only declares a placeholder item (so the submenu
// is never empty at layout time); the real entries - existing playlists first, then
// "+ New playlist..." - are rebuilt on every open, same reasoning as
// LibraryView.axaml.cs's OnPlaylistsFlyoutOpening.
internal static class AddToPlaylistMenu
{
    public static void Rebuild(MenuItem submenu, PlaylistsViewModel playlists, Action<string> addToPlaylist)
    {
        submenu.Items.Clear();

        foreach (var playlist in playlists.Playlists)
        {
            var playlistId = playlist.Id;
            var playlistItem = new MenuItem { Header = playlist.Name };
            playlistItem.Click += (_, _) => addToPlaylist(playlistId);
            submenu.Items.Add(playlistItem);
        }

        if (playlists.Playlists.Count > 0)
        {
            submenu.Items.Add(new Separator());
        }

        var newPlaylistItem = new MenuItem { Header = Strings.NewPlaylistEllipsis };
        newPlaylistItem.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(submenu) is not Window window)
                return;

            var name = await TextPromptWindow.ShowAsync(window, Strings.NewPlaylistPromptTitle, Strings.PlaylistNamePrompt, initialValue: null);
            if (name is not null)
            {
                addToPlaylist(playlists.CreatePlaylist(name));
            }
        };
        submenu.Items.Add(newPlaylistItem);
    }
}
