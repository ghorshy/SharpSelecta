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
            // Cached separately since Load()/Volume may be set before InitializeAsync has
            // created the mixer (e.g. the ViewModel's default volume, applied at construction).
            _pendingVolume = value;
            _mixer?.MasterVolume = value;
        }
    }

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices() =>
        OwnaudioNet.GetOutputDevices()
            .Where(d => d.IsOutput)
            .Select(d => new AudioOutputDevice(d.Name, d.IsDefault))
            .ToList();

    public void SetOutputDevice(string? deviceName)
    {
        if (OwnaudioNet.Engine is not { } engine)
        {
            throw new InvalidOperationException($"{nameof(OwnAudioEngine)} must be initialized before selecting an output device.");
        }

        // SetOutputDeviceByName is the only runtime device-switch API OwnaudioSharp exposes (unlike
        // AudioConfig.OutputDeviceId, which only takes effect at Initialize time) - null resolves to
        // whichever device the OS currently reports as default, rather than a fixed name, so it keeps
        // tracking the system default if that changes later.
        var targetDeviceName = deviceName ?? GetOutputDevices().FirstOrDefault(d => d.IsDefault)?.Name;
        if (targetDeviceName is null)
        {
            return;
        }

        if (engine.SetOutputDeviceByName(targetDeviceName))
        {
            logger.LogInformation("Switched output device to {DeviceName}", targetDeviceName);
        }
        else
        {
            logger.LogWarning("Failed to switch output device to {DeviceName}", targetDeviceName);
        }
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

        // Guards against disposing when InitializeAsync never ran (_mixer stays null in that
        // case) — OwnaudioNet.Shutdown() would have nothing to release.
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
