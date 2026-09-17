using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Collections;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class PlaylistsViewModel : ObservableObject
{
    private readonly LibraryViewModel _library;
    private readonly string _settingsFilePath;

    public BulkObservableCollection<PlaylistSummaryViewModel> Playlists { get; } = [];

    public BulkObservableCollection<LibraryTrackViewModel> Tracks { get; } = [];

    [ObservableProperty]
    private string? selectedPlaylistId;

    public PlaylistsViewModel(LibraryViewModel library, string settingsFilePath)
    {
        _library = library;
        _settingsFilePath = settingsFilePath;
        RefreshPlaylists();
    }

    public void RefreshPlaylists()
    {
        var summaries = LibraryIndexStore.ListPlaylists(_settingsFilePath);
        Playlists.ReplaceAll(summaries.Select(s => new PlaylistSummaryViewModel(s.Id, s.Name)));
    }

    public void SelectPlaylist(string? playlistId)
    {
        SelectedPlaylistId = playlistId;
        RefreshTracks();
    }

    private void RefreshTracks()
    {
        if (SelectedPlaylistId is not { } playlistId)
        {
            Tracks.ReplaceAll([]);
            return;
        }

        var entries = LibraryIndexStore.GetPlaylistTracks(_settingsFilePath, playlistId);
        Tracks.ReplaceAll(entries.Select(e => e.Track is { } track
            ? new LibraryTrackViewModel(track, _library)
            : new LibraryTrackViewModel(MissingTrackPlaceholder(e.FilePath), _library, isMissing: true)));
    }

    // A synthetic Track for a PlaylistTracks row whose file no longer has a matching Tracks row -
    // lets it render through the exact same LibraryTrackViewModel/TrackListView machinery as a
    // real track, just visibly labeled and with playback actions disabled (see the row's ContextMenu).
    private static Track MissingTrackPlaceholder(string filePath) =>
        new(filePath, $"(missing file) {Path.GetFileName(filePath)}");

    public string CreatePlaylist(string name)
    {
        var id = LibraryIndexStore.CreatePlaylist(_settingsFilePath, name);
        RefreshPlaylists();
        SelectPlaylist(id);
        return id;
    }

    public void RenamePlaylist(string playlistId, string newName)
    {
        LibraryIndexStore.RenamePlaylist(_settingsFilePath, playlistId, newName);
        RefreshPlaylists();
    }

    public void DeletePlaylist(string playlistId)
    {
        LibraryIndexStore.DeletePlaylist(_settingsFilePath, playlistId);
        RefreshPlaylists();
        if (SelectedPlaylistId == playlistId)
        {
            SelectPlaylist(null);
        }
    }

    public void AddTracksToSelectedPlaylist(IReadOnlyList<Track> tracks)
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        var currentPaths = LibraryIndexStore.GetPlaylistTracks(_settingsFilePath, playlistId).Select(e => e.FilePath).ToList();
        currentPaths.AddRange(tracks.Select(t => t.FilePath));
        LibraryIndexStore.ReplacePlaylistTracks(_settingsFilePath, playlistId, currentPaths);
        RefreshTracks();
    }

    public void RemoveFromSelectedPlaylist(Track track)
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        var currentPaths = LibraryIndexStore.GetPlaylistTracks(_settingsFilePath, playlistId).Select(e => e.FilePath).ToList();
        var index = currentPaths.IndexOf(track.FilePath);
        if (index < 0)
            return;

        currentPaths.RemoveAt(index);
        LibraryIndexStore.ReplacePlaylistTracks(_settingsFilePath, playlistId, currentPaths);
        RefreshTracks();
    }

    public void ReorderSelectedPlaylist(IReadOnlyList<string> filePathsInOrder)
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        LibraryIndexStore.ReplacePlaylistTracks(_settingsFilePath, playlistId, filePathsInOrder);
        RefreshTracks();
    }
}
