using Microsoft.Extensions.Logging;
using OwnaudioNET;
using OwnaudioNET.Mixing;
using OwnaudioNET.Sources;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class OwnAudioEngine(ILogger<OwnAudioEngine> logger) : IAudioEngine
{
    private AudioMixer? _mixer;
    private FileSource? _currentTrack;
    private float _pendingVolume = 1.0f;

    public async Task InitializeAsync()
    {
        await OwnaudioNet.InitializeAsync();
        OwnaudioNet.Start();
        _mixer = new AudioMixer(OwnaudioNet.Engine!.UnderlyingEngine);
        _mixer.Start();
        _mixer.MasterVolume = _pendingVolume;

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
            _currentTrack = null;

            _mixer.MasterClock.Reset();
        }

        logger.LogInformation("Loading {FilePath}", filePath);

        _currentTrack = new FileSource(filePath);
        _currentTrack.AttachToClock(_mixer.MasterClock);
        _currentTrack.Seek(0);

        _mixer.AddSourcePrepared(_currentTrack);
    }

    public void Play() => _currentTrack?.Play();

    public void Pause() => _currentTrack?.Pause();

    public void Seek(double positionSeconds) => _currentTrack?.Seek(positionSeconds);

    public double Position => _currentTrack?.Position ?? 0.0;

    public double Duration => _currentTrack?.Duration ?? 0.0;

    public float Volume
    {
        get => _mixer?.MasterVolume ?? _pendingVolume;
        set
        {
            _pendingVolume = value;
            _mixer?.MasterVolume = value;
        }
    }

    private static readonly string[] VirtualDeviceNameFragments =
    [
        "JACK Audio Connection Kit",
        "PipeWire Sound Server",
        "PulseAudio Sound Server",
        "Default ALSA Output",
        "Discard all samples",
    ];

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices() =>
        OwnaudioNet.GetOutputDevices()
            .Where(d => d.IsOutput && !IsVirtualDevice(d.Name))
            .DistinctBy(d => d.Name)
            .Select(d => new AudioOutputDevice(d.Name, d.IsDefault))
            .ToList();

    private static bool IsVirtualDevice(string name) =>
        VirtualDeviceNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public void SetOutputDevice(string? deviceName)
    {
        if (OwnaudioNet.Engine is not { } engine)
        {
            throw new InvalidOperationException($"{nameof(OwnAudioEngine)} must be initialized before selecting an output device.");
        }

        var targetDeviceName = deviceName ?? ResolveSystemDefaultDeviceName();
        if (targetDeviceName is null)
        {
            logger.LogWarning("Could not resolve a system default output device to switch to");
            return;
        }

        try
        {
            engine.Stop();
            if (engine.SetOutputDeviceByName(targetDeviceName))
            {
                logger.LogInformation("Switched output device to {DeviceName}", targetDeviceName);
            }
            else
            {
                logger.LogWarning("Failed to switch output device to {DeviceName}", targetDeviceName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to switch output device to {DeviceName}", targetDeviceName);
        }
        finally
        {
            try
            {
                engine.Start();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restart the audio engine after switching output device");
            }
        }
    }

    private static string? ResolveSystemDefaultDeviceName()
    {
        var rawDevices = OwnaudioNet.GetOutputDevices().Where(d => d.IsOutput).ToList();
        return rawDevices.FirstOrDefault(d => d.IsDefault)?.Name
            ?? rawDevices.FirstOrDefault(d => d.Name.Contains("Default ALSA Output", StringComparison.OrdinalIgnoreCase))?.Name;
    }

    public void Dispose()
    {
        if (_currentTrack is not null)
        {
            _currentTrack.Stop();
            _currentTrack.DetachFromClock();
            _mixer?.RemoveSource(_currentTrack);
            _currentTrack.Dispose();
            _currentTrack = null;
        }

        if (_mixer is not null)
        {
            _mixer.Stop();
            _mixer.Dispose();
            _mixer = null;
            OwnaudioNet.Shutdown();
        }

        logger.LogInformation("OwnAudioSharp engine disposed");
    }
}
