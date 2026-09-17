using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class LibraryView : UserControl
{
    // Playlist entries built by OnPlaylistsFlyoutOpening - tracked so they can be
    // removed and rebuilt fresh each time the flyout opens (list may have changed).
    private readonly List<MenuItem> _dynamicPlaylistItems = [];

    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        vm.SearchFocusRequested += (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }

    // MenuItem.ItemsSource nests its items into a submenu rather than presenting them
    // as flat siblings, so the Playlists ▾ list is built here instead, each time the
    // flyout opens, to reflect the current playlist set.
    private void OnPlaylistsFlyoutOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || DataContext is not LibraryViewModel vm)
            return;

        foreach (var item in _dynamicPlaylistItems)
        {
            flyout.Items.Remove(item);
        }
        _dynamicPlaylistItems.Clear();

        foreach (var playlist in vm.Playlists.Playlists)
        {
            var renameItem = new MenuItem { Header = Strings.Rename, Tag = playlist };
            renameItem.Click += OnRenamePlaylistClick;

            var deleteItem = new MenuItem { Header = Strings.Delete, Tag = playlist };
            deleteItem.Click += OnDeletePlaylistClick;

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(renameItem);
            contextMenu.Items.Add(deleteItem);

            var playlistItem = new MenuItem { Header = playlist.Name, Tag = playlist, ContextMenu = contextMenu };
            playlistItem.Click += OnSelectPlaylistClick;

            flyout.Items.Add(playlistItem);
            _dynamicPlaylistItems.Add(playlistItem);
        }
    }

    private async void OnNewPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<Window>() is not { } window || DataContext is not LibraryViewModel vm)
            return;

        var name = await TextPromptWindow.ShowAsync(window, Strings.NewPlaylistPromptTitle, initialValue: null);
        if (name is not null)
        {
            vm.Playlists.CreatePlaylist(name);
        }
    }

    private void OnSelectPlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PlaylistSummaryViewModel playlist } && DataContext is LibraryViewModel vm)
        {
            vm.Playlists.SelectPlaylist(playlist.Id);
            vm.LibrarySection = LibrarySection.Playlist;
        }
    }

    private async void OnRenamePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: PlaylistSummaryViewModel playlist } || this.FindAncestorOfType<Window>() is not { } window || DataContext is not LibraryViewModel vm)
            return;

        var newName = await TextPromptWindow.ShowAsync(window, Strings.RenamePlaylistPromptTitle, playlist.Name);
        if (newName is not null)
        {
            vm.Playlists.RenamePlaylist(playlist.Id, newName);
        }
    }

    private async void OnDeletePlaylistClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: PlaylistSummaryViewModel playlist } || this.FindAncestorOfType<Window>() is not { } window || DataContext is not LibraryViewModel vm)
            return;

        var confirmed = await ConfirmationWindow.ShowAsync(window, Strings.DeletePlaylistConfirmTitle, Strings.DeletePlaylistConfirmMessage);
        if (confirmed)
        {
            vm.Playlists.DeletePlaylist(playlist.Id);
        }
    }
}
