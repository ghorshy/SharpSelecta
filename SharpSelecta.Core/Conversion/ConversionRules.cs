namespace SharpSelecta.Core.Conversion;

// What may be converted to what - kept here rather than in a converter so the rules hold whichever
// encoder is behind IAudioConverter.
public static class ConversionRules
{
    private static readonly int[] LossyBitrates = [320, 256, 224, 192, 160, 128, 96, 64];

    // WAV tagging is poor and inconsistent between players, so a WAV only gets title, artist and album -
    // no other fields, credits or cover art.
    private static readonly Dictionary<ConversionTarget, ConversionFormat> Formats = new()
    {
        [ConversionTarget.Mp3] = new(".mp3", IsLossy: true, KeepsAllTags: true, _ => false),
        [ConversionTarget.Flac] = new(".flac", IsLossy: false, KeepsAllTags: true, codec => codec == "flac"),
        [ConversionTarget.AlacM4a] = new(".m4a", IsLossy: false, KeepsAllTags: true, codec => codec == "alac"),
        [ConversionTarget.AacM4a] = new(".m4a", IsLossy: true, KeepsAllTags: true, _ => false),
        [ConversionTarget.OggVorbis] = new(".ogg", IsLossy: true, KeepsAllTags: true, codec => codec == "vorbis"),
        [ConversionTarget.Wav] = new(".wav", IsLossy: false, KeepsAllTags: false, codec => IsPcm(codec) && !IsBigEndian(codec)),
        [ConversionTarget.Aiff] = new(".aiff", IsLossy: false, KeepsAllTags: true, IsBigEndian),
    };

    private static ConversionFormat Format(ConversionTarget target) => Formats[target];

    public static bool IsLossy(ConversionTarget target) => Format(target).IsLossy;

    // The reason a conversion isn't allowed, or null when it is.
    public static ConversionBlock? Check(SourceInfo source, ConversionTarget target)
    {
        if (!source.IsLossless && !IsLossy(target))
            return ConversionBlock.LossyToLossless;

        if (source.IsLossless && Format(target).IsSameCodec(source.Codec))
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

    // WAV holds little-endian PCM and AIFF big-endian, which is how a probed codec name tells them apart.
    public static bool IsPcm(string codec) => codec.StartsWith("pcm_", StringComparison.Ordinal);

    public static bool IsBigEndian(string codec) => IsPcm(codec) && codec.Contains("be", StringComparison.Ordinal);

    public static string Extension(ConversionTarget target) => Format(target).Extension;

    public static bool KeepsAllTags(ConversionTarget target) => Format(target).KeepsAllTags;

    private sealed record ConversionFormat(string Extension, bool IsLossy, bool KeepsAllTags, Func<string, bool> IsSameCodec);
}

public enum ConversionBlock
{
    LossyToLossless,
    SameFormat,
    NoLowerBitrate,
}
