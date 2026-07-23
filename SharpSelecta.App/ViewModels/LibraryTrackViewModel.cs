using SharpSelecta.App.Formatting;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Row-level wrapper so the Library's DataTemplate can reach the owning LibraryViewModel's
// commands directly (strongly typed, never null) instead of climbing the visual tree via
// $parent — which only exposes DataContext as an untyped, nullable object. Also where Track's
// raw numeric fields (Duration, SampleRate, BitDepth, Bitrate) get turned into the grid's display
// strings — that formatting doesn't belong on the domain record itself.
public sealed class LibraryTrackViewModel(Track track, LibraryViewModel library)
{
    public Track Track { get; } = track;

    public string DisplayName => Track.DisplayName;

    public string LengthDisplay => TrackFormatting.FormatDuration(Track.Duration);

    public string SampleRateDisplay => TrackFormatting.FormatSampleRate(Track.SampleRate);

    public string BitDepthDisplay => TrackFormatting.FormatBitDepth(Track.BitDepth);

    public string BitrateDisplay => TrackFormatting.FormatBitrate(Track.Bitrate);

    public LibraryViewModel Library { get; } = library;
}
