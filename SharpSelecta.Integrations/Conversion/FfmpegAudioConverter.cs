using FFMpegCore;
using SharpSelecta.Core.Conversion;

namespace SharpSelecta.Integrations.Conversion;

// FFmpeg is an optional external tool found on PATH - it isn't bundled.
public sealed class FfmpegAudioConverter : IAudioConverter
{
    private static readonly HashSet<string> LosslessCodecs = ["flac", "alac", "wavpack", "ape", "tta", "mlp", "truehd"];

    private readonly Lazy<bool> _isAvailable = new(() => IsOnPath("ffmpeg") && IsOnPath("ffprobe"));

    public bool IsAvailable => _isAvailable.Value;

    public async Task<SourceInfo?> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            return null;

        try
        {
            var analysis = await FFProbe.AnalyseAsync(path, cancellationToken: cancellationToken);
            var stream = analysis.PrimaryAudioStream;
            if (stream is not { CodecName: { } codecName, SampleRateHz: { } sampleRate, Channels: { } channels })
                return null;

            var codec = codecName.ToLowerInvariant();
            var bitrate = stream.BitRate is > 0 ? stream.BitRate : analysis.Format.BitRate;
            return new SourceInfo(
                codec,
                ConversionRules.IsPcm(codec) || LosslessCodecs.Contains(codec),
                bitrate is > 0 ? (int)Math.Round(bitrate.Value / 1000.0) : null,
                sampleRate,
                stream.BitDepth is > 0 ? stream.BitDepth : null,
                channels,
                analysis.Duration);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    public async Task ConvertAsync(ConversionRequest request, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var result = await FFMpegArguments
            .FromFileInput(request.SourcePath)
            .OutputToFile(request.TargetPath, overwrite: false, options => options
                .WithCustomArgument("-vn -map_metadata -1")
                .WithCustomArgument(CodecArguments(request)))
            .NotifyOnPercentageProgress(percent => progress.Report(percent / 100.0), request.Source.Duration)
            .CancellableThrough(cancellationToken, TimeSpan.Zero)
            .ProcessAsynchronously();

        if (!result.Success)
            throw new InvalidOperationException($"FFmpeg failed to convert '{request.SourcePath}'.");

        progress.Report(1);
    }

    private static string CodecArguments(ConversionRequest request) => request.Target switch
    {
        ConversionTarget.Flac => "-c:a flac",
        ConversionTarget.AlacM4a => "-c:a alac",
        ConversionTarget.AacM4a => $"-c:a aac -b:a {RequireBitrate(request)}k",
        ConversionTarget.Mp3 => $"-c:a libmp3lame -b:a {RequireBitrate(request)}k",
        ConversionTarget.OggVorbis => $"-c:a libvorbis -b:a {RequireBitrate(request)}k",
        ConversionTarget.Aiff => request.Source.BitDepth is > 16 ? "-c:a pcm_s24be" : "-c:a pcm_s16be",
        ConversionTarget.Wav => request.Source.BitDepth is > 16 ? "-c:a pcm_s24le" : "-c:a pcm_s16le",
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Target, null),
    };

    private static int RequireBitrate(ConversionRequest request) =>
        request.BitrateKbps ?? throw new ArgumentException("A lossy target needs a bitrate.", nameof(request));

    private static bool IsOnPath(string executable)
    {
        var name = OperatingSystem.IsWindows() ? executable + ".exe" : executable;
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return directories.Any(directory => File.Exists(Path.Combine(directory, name)));
    }
}
