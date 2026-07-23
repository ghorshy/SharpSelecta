using System;
using System.Globalization;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Formatting;

// Track (Core) is a plain domain record — display formatting (units, time layout) is a
// presentation concern and lives here instead, alongside Strings.resx and the other i18n-facing
// App-layer formatting (e.g. PlaybackControlsViewModel's position/duration display).
public static class TrackFormatting
{
    public static string FormatDuration(TimeSpan duration) => duration.Hours > 0
        ? duration.ToString(@"h\:mm\:ss")
        : duration.ToString(@"m\:ss");

    // ATL.NET reports -1 for BitDepth when the format has no fixed bit depth (e.g. lossy MP3/M4A) —
    // show nothing rather than a confusing "-1 Bit" in that case.
    public static string FormatBitDepth(int bitDepth) => bitDepth > 0 ? $"{bitDepth} Bit" : string.Empty;

    public static string FormatSampleRate(int sampleRate) => sampleRate > 0
        ? $"{(sampleRate / 1000.0).ToString("0.##", CultureInfo.InvariantCulture)} kHz"
        : string.Empty;

    public static string FormatBitrate(int bitrate) => bitrate > 0 ? $"{bitrate} kbps" : string.Empty;

    public static string TechnicalSummary(Track track) =>
        $"{track.FileType} {FormatSampleRate(track.SampleRate)}, {FormatBitrate(track.Bitrate)}, {FormatDuration(track.Duration)}";
}
