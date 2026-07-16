using System.Globalization;

namespace SharpSelecta.Core.Library;

public sealed record Track(string FilePath, string DisplayName)
{
    public int? TrackNumber { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public int? Year { get; init; }
    public TimeSpan Duration { get; init; }
    public int SampleRate { get; init; }
    public int BitDepth { get; init; }
    public int Bitrate { get; init; }
    public string? FileType { get; init; }

    public string LengthDisplay => Duration.Hours > 0
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");

    // ATL.NET reports -1 for BitDepth when the format has no fixed bit depth (e.g. lossy MP3/M4A) —
    // show nothing rather than a confusing "-1 Bit" in that case.
    public string BitDepthDisplay => BitDepth > 0 ? $"{BitDepth} Bit" : string.Empty;

    public string SampleRateDisplay => SampleRate > 0
        ? $"{(SampleRate / 1000.0).ToString("0.##", CultureInfo.InvariantCulture)} kHz"
        : string.Empty;

    public string BitrateDisplay => Bitrate > 0 ? $"{Bitrate} kbps" : string.Empty;
}
