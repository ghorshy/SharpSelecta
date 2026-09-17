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

    // Keeps the current selection restorable across restarts - see LibraryViewModel.InitializeAsync.
    partial void OnSelectedPlaylistIdChanged(string? value) =>
        SettingsStore.SaveSelectedPlaylistId(_settingsFilePath, value);

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
    // real track, grayed out with only "Remove from playlist" enabled (see TrackListView.axaml).
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

    // Creates a new playlist from an M3U's paths, keeping only those already in the library
    // (in file order) and reporting how many were skipped for the caller's status message.
    public int ImportPlaylistFromM3u(string filePath, string content)
    {
        var paths = M3uPlaylistFile.ParsePaths(content, Path.GetDirectoryName(filePath) ?? "");
        var indexedPaths = new HashSet<string>(_library.Tracks.Select(t => t.Track.FilePath));
        var matched = paths.Where(indexedPaths.Contains).ToList();
        var skippedCount = paths.Count - matched.Count;

        var playlistName = Path.GetFileNameWithoutExtension(filePath);
        CreatePlaylist(playlistName);
        ReorderSelectedPlaylist(matched);

        return skippedCount;
    }

    // Entries whose file is missing (Track is null) are excluded - an M3U pointing at a
    // nonexistent file is useless, and the entry carries no duration/artist/title to write.
    public string ExportPlaylistToM3u(string playlistId)
    {
        var entries = LibraryIndexStore.GetPlaylistTracks(_settingsFilePath, playlistId);
        var tracks = entries
            .Where(entry => entry.Track is not null)
            .Select(entry => (entry.FilePath, entry.Track!.Duration, entry.Track.Artist ?? "", entry.Track.Title ?? ""))
            .ToList();

        return M3uPlaylistFile.Write(tracks);
    }
}
