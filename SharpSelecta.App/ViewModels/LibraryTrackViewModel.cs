using SharpSelecta.App.Formatting;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

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
