namespace SharpSelecta.Core.Conversion;

// What may be converted to what - kept here rather than in a converter so the rules hold whichever
// encoder is behind IAudioConverter.
public static class ConversionRules
{
    private static readonly int[] LossyBitrates = [320, 256, 224, 192, 160, 128, 96, 64];

    public static bool IsLossy(ConversionTarget target) => target is ConversionTarget.Mp3 or ConversionTarget.AacM4a or ConversionTarget.OggVorbis;

    // The reason a conversion isn't allowed, or null when it is.
    public static ConversionBlock? Check(SourceInfo source, ConversionTarget target)
    {
        if (!source.IsLossless && !IsLossy(target))
            return ConversionBlock.LossyToLossless;

        if (source.IsLossless && IsSameCodec(source, target))
            return ConversionBlock.SameFormat;

        if (!source.IsLossless && AllowedBitrates(source, target).Count == 0)
            return ConversionBlock.NoLowerBitrate;

        return null;
    }

    // Empty for the lossless targets. A lossy source is never offered a bitrate above its own: re-encoding
    // 128 kbps as 320 would only make the file bigger, not better.
    public static IReadOnlyList<int> AllowedBitrates(SourceInfo source, ConversionTarget target)
    {
        if (!IsLossy(target))
            return [];

        if (source.IsLossless)
            return LossyBitrates;

        return source.BitrateKbps is { } ceiling ? LossyBitrates.Where(bitrate => bitrate <= ceiling).ToList() : [];
    }

    private static bool IsSameCodec(SourceInfo source, ConversionTarget target) => target switch
    {
        ConversionTarget.Flac => source.Codec == "flac",
        ConversionTarget.AlacM4a => source.Codec == "alac",
        ConversionTarget.Wav => IsPcm(source.Codec) && !IsBigEndian(source.Codec),
        ConversionTarget.Aiff => IsPcm(source.Codec) && IsBigEndian(source.Codec),
        ConversionTarget.OggVorbis => source.Codec == "vorbis",
        _ => false,
    };

    // WAV holds little-endian PCM and AIFF big-endian, which is how a probed codec name tells them apart.
    public static bool IsPcm(string codec) => codec.StartsWith("pcm_", StringComparison.Ordinal);

    public static bool IsBigEndian(string codec) => IsPcm(codec) && codec.Contains("be", StringComparison.Ordinal);

    public static string Extension(ConversionTarget target) => target switch
    {
        ConversionTarget.Mp3 => ".mp3",
        ConversionTarget.Flac => ".flac",
        ConversionTarget.AlacM4a or ConversionTarget.AacM4a => ".m4a",
        ConversionTarget.OggVorbis => ".ogg",
        ConversionTarget.Wav => ".wav",
        ConversionTarget.Aiff => ".aiff",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    // WAV tagging is poor and inconsistent between players, so a WAV only gets title, artist and album -
    // no other fields, credits or cover art.
    public static bool KeepsAllTags(ConversionTarget target) => target != ConversionTarget.Wav;
}

public enum ConversionBlock
{
    LossyToLossless,
    SameFormat,
    NoLowerBitrate,
}
