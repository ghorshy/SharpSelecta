using System;
using System.Globalization;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Formatting;

public static class TrackFormatting
{
    public static string FormatDuration(TimeSpan duration) => duration.Hours > 0
        ? duration.ToString(@"h\:mm\:ss")
        : duration.ToString(@"m\:ss");

    public static string FormatBitDepth(int bitDepth) => bitDepth > 0 ? $"{bitDepth} Bit" : string.Empty;

    public static string FormatSampleRate(int sampleRate) => sampleRate > 0
        ? $"{(sampleRate / 1000.0).ToString("0.##", CultureInfo.InvariantCulture)} kHz"
        : string.Empty;

    public static string FormatBitrate(int bitrate) => bitrate > 0 ? $"{bitrate} kbps" : string.Empty;

    public static string TechnicalSummary(Track track) =>
        $"{track.FileType} {FormatSampleRate(track.SampleRate)}, {FormatBitrate(track.Bitrate)}, {FormatDuration(track.Duration)}";

    public static string ArtistTitleLabel(string? artist, string displayName) =>
        string.IsNullOrWhiteSpace(artist) ? displayName : $"{artist} - {displayName}";
}
