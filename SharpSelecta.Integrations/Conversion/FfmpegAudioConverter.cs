using System.Diagnostics;
using FFMpegCore;
using SharpSelecta.Core.Conversion;

namespace SharpSelecta.Integrations.Conversion;

// FFmpeg is an optional external tool found on PATH - it isn't bundled.
public sealed class FfmpegAudioConverter : IAudioConverter
{
    private readonly Lazy<bool> _isAvailable = new(() => IsOnPath("ffmpeg") && IsOnPath("ffprobe"));

    public bool IsAvailable => _isAvailable.Value;

    public async Task<SourceInfo?> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            return null;

        var startInfo = new ProcessStartInfo("ffprobe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in (string[])["-loglevel", "error", "-print_format", "json", "-show_format", "-show_streams", path])
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            using var kill = cancellationToken.Register(() => Kill(process));
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            _ = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? FfprobeOutput.ToSourceInfo(await output) : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
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
