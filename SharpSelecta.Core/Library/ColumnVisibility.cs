namespace SharpSelecta.Core.Library;

public sealed record ColumnVisibility(
    bool TrackNumber,
    bool Title,
    bool Artist,
    bool Album,
    bool Length,
    bool SampleRate,
    bool BitDepth,
    bool Bitrate,
    bool FileType,
    bool Year);
