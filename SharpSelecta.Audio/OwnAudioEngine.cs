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

    // OwnaudioSharp's Linux/cpal-based ALSA enumeration is extremely noisy: alongside real hardware
    // ports it lists virtual routing endpoints (the PipeWire/PulseAudio/JACK servers themselves,
    // ALSA's "null" device, and an ALSA "Default Output" alias that just redirects to whichever of
    // those is currently active) - none of that is useful to pick from directly, since the "System
    // Default" entry in Settings already covers letting the OS/PipeWire route it. Confirmed against
    // a real desktop via a throwaway diagnostic: of 33 raw entries, only these 5 name fragments
    // accounted for every non-hardware one.
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
            // The same physical port is also listed multiple times under different chmap/plughw
            // variants with an identical Name (same diagnostic: every HDMI/analog port appeared
            // twice, only MaxOutputChannels differed) - they all resolve to the same
            // SetOutputDeviceByName(name) target anyway, so only the first is kept.
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

        // SetOutputDeviceByName is the only runtime device-switch API OwnaudioSharp exposes (unlike
        // AudioConfig.OutputDeviceId, which only takes effect at Initialize time) - null resolves to
        // whichever device the OS currently reports as default, rather than a fixed name, so it keeps
        // tracking the system default if that changes later.
        var targetDeviceName = deviceName ?? ResolveSystemDefaultDeviceName();
        if (targetDeviceName is null)
        {
            logger.LogWarning("Could not resolve a system default output device to switch to");
            return;
        }

        // This whole sequence turned out considerably more fragile than the docs suggest, confirmed
        // via a throwaway diagnostic against real hardware: SetOutputDeviceByName (1) throws if
        // called while the engine is running rather than accepting a live switch, so Stop() first is
        // mandatory; (2) can itself throw - not just return false - when the target device rejects
        // the current stream config (one onboard analog output required a fixed 1024-frame buffer
        // against our default config's 512); and (3) even the *bracketing* engine.Stop() call was
        // observed to throw AudioEngineException on its own in the same diagnostic (a second
        // Stop()-switch-Start() cycle right after a successful one). None of that should ever crash
        // playback control - every step is caught independently, and Start() is always attempted in
        // a finally (with its own catch) so a failed switch doesn't leave the engine stuck stopped.
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

    // IsDefault is unreliable on Linux/cpal's ALSA backend - confirmed via the same diagnostic as
    // GetOutputDevices() above that it was false for every one of 33 raw devices on a real PipeWire
    // desktop, despite the docs describing it as "system default device for its type" (likely
    // populated correctly on backends with a real default-device query, e.g. Windows/macOS). Falls
    // back to ALSA's own dynamic "Default ALSA Output" alias, which resolves at the OS level to
    // whatever's actually active and was confirmed switchable via SetOutputDeviceByName in the same
    // diagnostic - deliberately read from the raw device list, not the filtered GetOutputDevices()
    // above, since that alias is intentionally hidden from the picker as a virtual entry.
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
