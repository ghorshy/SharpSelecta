using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class AlbumViewModel(
    string title,
    string artist,
    int? year,
    IReadOnlyList<LibraryTrackViewModel> tracks,
    LibraryViewModel library) : ObservableObject
{
    public string Title { get; } = title;

    public string Artist { get; } = artist;

    public int? Year { get; } = year;

    public DateTime DateAdded { get; } = tracks.Max(t => t.Track.DateAddedUtc);

    public IReadOnlyList<LibraryTrackViewModel> Tracks { get; } = tracks;

    public IReadOnlyList<Track> UnderlyingTracks { get; } = tracks.Select(t => t.Track).ToList();

    public IReadOnlyList<AlbumTrackRowViewModel> TrackRows { get; } = BuildTrackRows(tracks);

    public LibraryViewModel Library { get; } = library;

    // e.g. "2024 · 9 tracks · 32:15"; the year is left out when no track has one.
    public string Summary => string.Join(Strings.MetaSeparator, new[]
    {
        Year?.ToString(CultureInfo.CurrentCulture),
        Strings.AlbumTrackCount(Tracks.Count),
        TrackFormatting.FormatDuration(TimeSpan.FromTicks(Tracks.Sum(t => t.Track.Duration.Ticks))),
    }.OfType<string>());

    [ObservableProperty] public partial byte[]? ArtworkBytes { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    private static IReadOnlyList<AlbumTrackRowViewModel> BuildTrackRows(IReadOnlyList<LibraryTrackViewModel> tracks)
    {
        var distinctArtistCount = tracks
            .Select(t => (t.Track.Artist ?? string.Empty).Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var common = ArtistList.Common(tracks.Select(t => t.Track.Artist));

        return
        [
            .. tracks
                .Select((t, index) =>
                {
                    // Artists the whole album shares are already in its label, so a track only adds the
                    // others (the guests); with nobody in common it shows all of its artists.
                    var shown = ArtistList.Split(t.Track.Artist)
                        .Where(artist => common.Count == 0
                            ? distinctArtistCount > 1
                            : !common.Contains(artist, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    var artistSuffix = shown.Count > 0 ? $"({string.Join(Strings.ArtistSeparator, shown)})" : null;
                    return new AlbumTrackRowViewModel(t, artistSuffix, index + 1);
                })
        ];
    }
}