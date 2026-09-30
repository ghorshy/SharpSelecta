using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

// Shared by every "Add to playlist" submenu. The static "+ New playlist..." entry and its Separator
// are declared in XAML (so the submenu is never empty at layout time); the per-playlist entries
// after them are rebuilt on every open, same reasoning as LibraryView.axaml.cs's OnPlaylistsFlyoutOpening.
internal static class AddToPlaylistMenu
{
    public static void RebuildPlaylistEntries(MenuItem submenu, PlaylistsViewModel playlists, Action<string> addToPlaylist)
    {
        while (submenu.Items.Count > 2)
        {
            submenu.Items.RemoveAt(submenu.Items.Count - 1);
        }

        foreach (var playlist in playlists.Playlists)
        {
            var playlistId = playlist.Id;
            var playlistItem = new MenuItem { Header = playlist.Name };
            playlistItem.Click += (_, _) => addToPlaylist(playlistId);
            submenu.Items.Add(playlistItem);
        }
    }

    public static async Task<string?> PromptForNewPlaylistAsync(Control owner, PlaylistsViewModel playlists)
    {
        if (TopLevel.GetTopLevel(owner) is not Window window)
            return null;

        var name = await TextPromptWindow.ShowAsync(window, Strings.NewPlaylistPromptTitle, Strings.PlaylistNamePrompt, initialValue: null);
        return name is null ? null : playlists.CreatePlaylist(name);
    }
}
