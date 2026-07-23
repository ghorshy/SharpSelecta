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
}
