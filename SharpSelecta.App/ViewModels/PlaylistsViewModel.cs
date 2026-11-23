using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Collections;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public partial class PlaylistsViewModel : ObservableObject
{
    private readonly LibraryViewModel _library;
    private readonly SettingsStore _settings;

    public BulkObservableCollection<PlaylistSummaryViewModel> Playlists { get; } = [];

    public BulkObservableCollection<LibraryTrackViewModel> Tracks { get; } = [];

    [ObservableProperty]
    private string? selectedPlaylistId;

    // The rows in their saved order; a drag only rearranges Tracks until Save order.
    private List<LibraryTrackViewModel> _savedOrder = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand), nameof(RevertOrderCommand))]
    public partial bool HasPendingOrder { get; private set; }

    public PlaylistsViewModel(LibraryViewModel library, SettingsStore settings)
    {
        _library = library;
        _settings = settings;
        RefreshPlaylists();
    }

    public void RefreshPlaylists()
    {
        var summaries = LibraryIndexStore.ListPlaylists(_settings.FilePath);
        Playlists.ReplaceAll(summaries.Select(s => new PlaylistSummaryViewModel(s.Id, s.Name)));
    }

    public void SelectPlaylist(string? playlistId)
    {
        SelectedPlaylistId = playlistId;
        RefreshTracks();
    }

    // Keeps the current selection restorable across restarts - see LibraryViewModel.InitializeAsync.
    partial void OnSelectedPlaylistIdChanged(string? value) =>
        _settings.Update(s => s with { SelectedPlaylistId = value });

    // Reloading from the index drops an unsaved order.
    public void RefreshTracks()
    {
        LoadTracks();
        _savedOrder = [.. Tracks];
        HasPendingOrder = false;
    }

    private void LoadTracks()
    {
        if (SelectedPlaylistId is not { } playlistId)
        {
            Tracks.SyncTo([]);
            return;
        }

        // Rows whose track didn't change are reused, so a rescan doesn't reset the playlist view.
        var reusable = Tracks
            .GroupBy(row => (row.Track, row.IsMissing))
            .ToDictionary(g => g.Key, g => new Queue<LibraryTrackViewModel>(g));

        var entries = LibraryIndexStore.GetPlaylistTracks(_settings.FilePath, playlistId);
        Tracks.SyncTo([.. entries.Select(e =>
        {
            var key = e.Track is { } track ? (track, false) : (MissingTrackPlaceholder(e.FilePath), true);
            return reusable.TryGetValue(key, out var rows) && rows.TryDequeue(out var row)
                ? row
                : new LibraryTrackViewModel(key.Item1, _library, key.Item2);
        })]);
    }

    // A synthetic Track for a PlaylistTracks row whose file no longer has a matching Tracks row -
    // lets it render through the exact same LibraryTrackViewModel/TrackListView machinery as a
    // real track, grayed out with only "Remove from playlist" enabled (see TrackListView.axaml).
    private static Track MissingTrackPlaceholder(string filePath) =>
        new(filePath, $"(missing file) {Path.GetFileName(filePath)}");

    public string CreatePlaylist(string name)
    {
        var id = LibraryIndexStore.CreatePlaylist(_settings.FilePath, name);
        RefreshPlaylists();
        SelectPlaylist(id);
        return id;
    }

    public void RenamePlaylist(string playlistId, string newName)
    {
        LibraryIndexStore.RenamePlaylist(_settings.FilePath, playlistId, newName);
        RefreshPlaylists();
    }

    public void DeletePlaylist(string playlistId)
    {
        LibraryIndexStore.DeletePlaylist(_settings.FilePath, playlistId);
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

        var currentPaths = LibraryIndexStore.GetPlaylistTracks(_settings.FilePath, playlistId).Select(e => e.FilePath).ToList();
        currentPaths.AddRange(tracks.Select(t => t.FilePath));
        LibraryIndexStore.ReplacePlaylistTracks(_settings.FilePath, playlistId, currentPaths);
        RefreshTracks();
    }

    public void RemoveFromSelectedPlaylist(Track track)
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        var currentPaths = LibraryIndexStore.GetPlaylistTracks(_settings.FilePath, playlistId).Select(e => e.FilePath).ToList();
        var index = currentPaths.IndexOf(track.FilePath);
        if (index < 0)
            return;

        currentPaths.RemoveAt(index);
        LibraryIndexStore.ReplacePlaylistTracks(_settings.FilePath, playlistId, currentPaths);
        RefreshTracks();
    }

    public void ReorderSelectedPlaylist(IReadOnlyList<string> filePathsInOrder)
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        LibraryIndexStore.ReplacePlaylistTracks(_settings.FilePath, playlistId, filePathsInOrder);
        RefreshTracks();
    }

    // Puts the rows (in their playlist order) before the row now at index, Tracks.Count being the end;
    // rows, not paths, so the right copy of a track that's in the playlist twice moves. Saved only by
    // SaveOrder, so a stray drag can't rearrange a playlist.
    public bool MoveTracks(IReadOnlyCollection<LibraryTrackViewModel> rows, int index)
    {
        if (SelectedPlaylistId is null)
            return false;

        var moving = new HashSet<LibraryTrackViewModel>(rows, ReferenceEqualityComparer.Instance);
        var moved = Tracks.Where(moving.Contains).ToList();
        if (moved.Count == 0)
            return false;

        var order = Tracks.Where(row => !moving.Contains(row)).ToList();
        order.InsertRange(Tracks.Take(Math.Clamp(index, 0, Tracks.Count)).Count(row => !moving.Contains(row)), moved);
        if (order.SequenceEqual(Tracks))
            return false;

        Tracks.SyncTo(order);
        HasPendingOrder = !order.SequenceEqual(_savedOrder);
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasPendingOrder))]
    private void SaveOrder()
    {
        if (SelectedPlaylistId is not { } playlistId)
            return;

        LibraryIndexStore.ReplacePlaylistTracks(_settings.FilePath, playlistId, [.. Tracks.Select(row => row.Track.FilePath)]);
        _savedOrder = [.. Tracks];
        HasPendingOrder = false;
    }

    [RelayCommand(CanExecute = nameof(HasPendingOrder))]
    private void RevertOrder()
    {
        Tracks.SyncTo(_savedOrder);
        HasPendingOrder = false;
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
        var entries = LibraryIndexStore.GetPlaylistTracks(_settings.FilePath, playlistId);
        var tracks = entries
            .Where(entry => entry.Track is not null)
            .Select(entry => (entry.FilePath, entry.Track!.Duration, entry.Track.Artist ?? "", entry.Track.Title ?? ""))
            .ToList();

        return M3uPlaylistFile.Write(tracks);
    }
}
