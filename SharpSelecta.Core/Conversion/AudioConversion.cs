namespace SharpSelecta.Core.Conversion;

public enum ConversionTarget
{
    Mp3,
    Flac,
    AlacM4a,
    AacM4a,
    OggVorbis,
    Wav,
    Aiff,
}

// Bitrate only matters for the lossy targets (Mp3, AacM4a); null otherwise.
public sealed record ConversionRequest(string SourcePath, string TargetPath, ConversionTarget Target, int? BitrateKbps);

// What a file is, as far as converting it goes. Codec is the lower-case codec name ("flac", "alac", "aac",
// "mp3", "pcm_s16le"...). BitrateKbps is null when the file doesn't say.
public sealed record SourceInfo(string Codec, bool IsLossless, int? BitrateKbps, int SampleRate, int? BitDepth, int Channels, TimeSpan Duration);

public interface IAudioConverter
{
    // False while the encoder isn't there (e.g. FFmpeg isn't installed).
    bool IsAvailable { get; }

    // Null when the file can't be read.
    Task<SourceInfo?> ProbeAsync(string path, CancellationToken cancellationToken);

    // Audio only: tags and cover art are carried over by the caller. Writes TargetPath; reports 0..1.
    Task ConvertAsync(ConversionRequest request, IProgress<double> progress, CancellationToken cancellationToken);
}
