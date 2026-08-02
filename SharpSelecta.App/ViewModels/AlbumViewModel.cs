using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class AlbumViewModel(string title, string artist, int? year, IReadOnlyList<LibraryTrackViewModel> tracks, LibraryViewModel library) : ObservableObject
{
    public string Title { get; } = title;

    public string Artist { get; } = artist;

    public int? Year { get; } = year;

    public IReadOnlyList<LibraryTrackViewModel> Tracks { get; } = tracks;

    public IReadOnlyList<Track> UnderlyingTracks { get; } = tracks.Select(t => t.Track).ToList();

    public IReadOnlyList<AlbumTrackRowViewModel> TrackRows { get; } = BuildTrackRows(tracks);

    public LibraryViewModel Library { get; } = library;

    [ObservableProperty]
    private byte[]? artworkBytes;

    private static IReadOnlyList<AlbumTrackRowViewModel> BuildTrackRows(IReadOnlyList<LibraryTrackViewModel> tracks)
    {
        var distinctArtistCount = tracks
            .Select(t => (t.Track.Artist ?? string.Empty).Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return tracks
            .Select(t =>
            {
                var trackArtist = (t.Track.Artist ?? string.Empty).Trim();
                var artistSuffix = distinctArtistCount > 1 && trackArtist.Length > 0 ? $"({trackArtist})" : null;
                return new AlbumTrackRowViewModel(t, artistSuffix);
            })
            .ToList();
    }
}
