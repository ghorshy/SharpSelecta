using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Conversion;

namespace SharpSelecta.Integrations.Conversion;

// ffprobe's JSON (-show_format -show_streams) read with a source-generated context, which NativeAOT allows
// (FFMpegCore's FFProbe reads it through reflection).
public static partial class FfprobeOutput
{
    private static readonly HashSet<string> LosslessCodecs = ["flac", "alac", "wavpack", "ape", "tta", "mlp", "truehd"];

    public static SourceInfo? ToSourceInfo(string json)
    {
        ProbeResult? result;
        try
        {
            result = JsonSerializer.Deserialize(json, ProbeJsonContext.Default.ProbeResult);
        }
        catch (JsonException)
        {
            return null;
        }

        if (result is null)
            return null;

        // The default stream first, then the lowest index, like FFMpegCore's PrimaryAudioStream.
        var stream = result.Streams
            .Where(s => s.CodecType == "audio")
            .OrderByDescending(s => s.Disposition?.GetValueOrDefault("default") == 1)
            .ThenBy(s => s.Index)
            .FirstOrDefault();
        if (stream is not { CodecName: { } codecName, Channels: { } channels }
            || !int.TryParse(stream.SampleRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleRate))
            return null;

        var codec = codecName.ToLowerInvariant();
        var bitrate = ParseLong(stream.BitRate) is > 0 and var streamBitrate ? streamBitrate : ParseLong(result.Format?.BitRate);
        var bitDepth = int.TryParse(stream.BitsPerRawSample, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawBits)
            ? rawBits
            : stream.BitsPerSample ?? 0;
        var duration = result.Streams
            .Where(s => s.CodecType is "audio" or "video" or "subtitle")
            .Select(StreamDuration)
            .Append(ParseDuration(result.Format?.Duration))
            .Max();

        return new SourceInfo(
            codec,
            ConversionRules.IsPcm(codec) || LosslessCodecs.Contains(codec),
            bitrate is > 0 ? (int)Math.Round(bitrate.Value / 1000.0) : null,
            sampleRate,
            bitDepth > 0 ? bitDepth : null,
            channels,
            duration);
    }

    // Matroska keeps a stream's length in a DURATION tag instead.
    private static TimeSpan StreamDuration(ProbeStream stream) =>
        ParseDuration(string.IsNullOrEmpty(stream.Duration)
            ? stream.Tags?.FirstOrDefault(tag => tag.Key.Equals("duration", StringComparison.OrdinalIgnoreCase)).Value
            : stream.Duration);

    // Seconds ("183.4") or h:mm:ss.fffffffff (tags); anything else ("N/A") is zero.
    private static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return TimeSpan.Zero;

        var parts = value.Split(':');
        var seconds = 0.0;
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return TimeSpan.Zero;

            seconds = seconds * 60 + number;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    internal sealed class ProbeResult
    {
        public List<ProbeStream> Streams { get; set; } = [];

        public ProbeFormat? Format { get; set; }
    }

    internal sealed class ProbeStream
    {
        public int Index { get; set; }

        public string? CodecType { get; set; }

        public string? CodecName { get; set; }

        public string? SampleRate { get; set; }

        public int? Channels { get; set; }

        public string? BitRate { get; set; }

        public string? BitsPerRawSample { get; set; }

        public int? BitsPerSample { get; set; }

        public string? Duration { get; set; }

        public Dictionary<string, int>? Disposition { get; set; }

        public Dictionary<string, string>? Tags { get; set; }
    }

    internal sealed class ProbeFormat
    {
        public string? Duration { get; set; }

        public string? BitRate { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    [JsonSerializable(typeof(ProbeResult))]
    internal sealed partial class ProbeJsonContext : JsonSerializerContext
    {
    }
}
