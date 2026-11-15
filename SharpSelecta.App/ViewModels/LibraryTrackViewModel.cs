using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Formatting;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed class LibraryTrackViewModel(Track track, LibraryViewModel library, bool isMissing = false) : ObservableObject
{
    public Track Track { get; private set; } = track;

    // In place rather than a new instance, so a selected row stays selected.
    public void Update(Track track)
    {
        Track = track;
        OnPropertyChanged(string.Empty);
    }

    public bool IsMissing { get; } = isMissing;

    public string DisplayName => Track.DisplayName;

    public string LengthDisplay => TrackFormatting.FormatDuration(Track.Duration);

    public string SampleRateDisplay => TrackFormatting.FormatSampleRate(Track.SampleRate);

    public string BitDepthDisplay => TrackFormatting.FormatBitDepth(Track.BitDepth);

    public string BitrateDisplay => TrackFormatting.FormatBitrate(Track.Bitrate);

    public string FileTypeDisplay => TrackFormatting.FormatFileType(Track.FileType);

    public LibraryViewModel Library { get; } = library;
}
