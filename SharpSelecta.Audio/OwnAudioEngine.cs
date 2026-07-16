using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ownaudio.Core.Common;
using OwnaudioNET;
using OwnaudioNET.Mixing;
using OwnaudioNET.Sources;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class OwnAudioEngine(ILogger<OwnAudioEngine> logger) : IAudioEngine
{
    private AudioMixer? _mixer;
    private FileSource? _currentTrack;
    private string? _transcodedTempFile;

    public async Task InitializeAsync()
    {
        await OwnaudioNet.InitializeAsync();
        OwnaudioNet.Start();
        _mixer = new AudioMixer(OwnaudioNet.Engine!.UnderlyingEngine);
        _mixer.Start();

        logger.LogInformation(
            "OwnAudioSharp engine initialized (SampleRate={SampleRate}, Channels={Channels})",
            OwnaudioNet.Engine.Config.SampleRate,
            OwnaudioNet.Engine.Config.Channels);
    }

    public void Load(string filePath)
    {
        if (_mixer is null)
        {
            throw new InvalidOperationException($"{nameof(OwnAudioEngine)} must be initialized before loading a file.");
        }

        if (_currentTrack is not null)
        {
            _currentTrack.Stop();
            _currentTrack.DetachFromClock();
            _mixer.RemoveSource(_currentTrack);
            _currentTrack.Dispose();
        }

        DeleteTranscodedTempFile();

        logger.LogInformation("Loading {FilePath}", filePath);

        try
        {
            _currentTrack = new FileSource(filePath);
        }
        catch (AudioException ex)
        {
            // OwnAudioSharp has no decoder for this format (e.g. ALAC, Apple's lossless
            // counterpart to FLAC) and no FFmpeg fallback of its own in this build — transcode
            // with the system ffmpeg binary to FLAC (lossless, and compact enough to be cheap
            // on tmpfs-backed temp dirs) and load the result instead.
            logger.LogWarning(ex, "Native decode failed for {FilePath}, falling back to ffmpeg transcode", filePath);
            _transcodedTempFile = TranscodeToFlac(filePath);
            _currentTrack = new FileSource(_transcodedTempFile);
        }

        _currentTrack.AttachToClock(_mixer.MasterClock);
        
        _mixer.AddSourcePrepared(_currentTrack);
    }

    public void Play() => _currentTrack?.Play();

    public void Pause() => _currentTrack?.Pause();

    private string TranscodeToFlac(string filePath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sharpselecta-{Guid.NewGuid():N}.flac");

        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("flac");
        startInfo.ArgumentList.Add(tempPath);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Failed to start ffmpeg.");

        // Drain both streams concurrently with WaitForExit to avoid a pipe-buffer deadlock —
        // ffmpeg writes a lot of progress/codec info to stderr.
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var stderr = stderrTask.GetAwaiter().GetResult();
            logger.LogError("ffmpeg exited with an error ({ExitCode}) transcoding {FilePath}: {Stderr}",
                process.ExitCode, filePath, stderr);
            throw new InvalidOperationException($"ffmpeg exited with an error ({process.ExitCode}): {stderr}");
        }

        _ = stdoutTask;
        return tempPath;
    }

    private void DeleteTranscodedTempFile()
    {
        if (_transcodedTempFile is null)
            return;

        File.Delete(_transcodedTempFile);
        _transcodedTempFile = null;
    }
}