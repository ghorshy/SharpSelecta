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
    private OwnaudioNET.Effects.EqualizerEffect? _equalizer;
    private OwnaudioNET.Effects.LimiterEffect? _limiter;

    private static readonly IReadOnlyList<int> StandardEqualizerBandFrequenciesHz =
        [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    public async Task InitializeAsync()
    {
        await OwnaudioNet.InitializeAsync();
        OwnaudioNet.Start();
        _mixer = new AudioMixer(OwnaudioNet.Engine!.UnderlyingEngine);
        _mixer.Start();
        _mixer.MasterVolume = _pendingVolume;

        _equalizer = new OwnaudioNET.Effects.EqualizerEffect(OwnaudioNET.Effects.EqualizerPreset.Default, OwnaudioNet.Engine.Config.SampleRate)
        {
            Enabled = false,
        };
        _mixer.AddMasterEffect(_equalizer);

        // EqualizerEffect has no headroom compensation, so boosting a band on already-hot
        // material hard-clips (verified empirically). This lookahead limiter sits right after
        // it as a near-inaudible safety net (threshold/ceiling just under 0 dBFS) and is toggled
        // together with the equalizer, so plain playback with the EQ off is untouched.
        _limiter = new OwnaudioNET.Effects.LimiterEffect(OwnaudioNet.Engine.Config.SampleRate, threshold: -0.3f, ceiling: -0.1f, release: 60f, lookAheadMs: 5f)
        {
            Enabled = false,
        };
        _mixer.AddMasterEffect(_limiter);

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

    public bool EqualizerEnabled
    {
        get => _equalizer?.Enabled ?? false;
        set
        {
            if (_equalizer is not null)
            {
                _equalizer.Enabled = value;
            }

            if (_limiter is not null)
            {
                _limiter.Enabled = value;
            }
        }
    }

    public IReadOnlyList<int> EqualizerBandFrequenciesHz => StandardEqualizerBandFrequenciesHz;

    public IReadOnlyList<float> EqualizerBandGainsDb =>
        _equalizer is null
            ? new float[10]
            :
            [
                _equalizer.Band0Gain, _equalizer.Band1Gain, _equalizer.Band2Gain, _equalizer.Band3Gain, _equalizer.Band4Gain,
                _equalizer.Band5Gain, _equalizer.Band6Gain, _equalizer.Band7Gain, _equalizer.Band8Gain, _equalizer.Band9Gain,
            ];

    public void SetEqualizerBandGain(int bandIndex, float gainDb)
    {
        if (_equalizer is null)
            return;

        switch (bandIndex)
        {
            case 0: _equalizer.Band0Gain = gainDb; break;
            case 1: _equalizer.Band1Gain = gainDb; break;
            case 2: _equalizer.Band2Gain = gainDb; break;
            case 3: _equalizer.Band3Gain = gainDb; break;
            case 4: _equalizer.Band4Gain = gainDb; break;
            case 5: _equalizer.Band5Gain = gainDb; break;
            case 6: _equalizer.Band6Gain = gainDb; break;
            case 7: _equalizer.Band7Gain = gainDb; break;
            case 8: _equalizer.Band8Gain = gainDb; break;
            case 9: _equalizer.Band9Gain = gainDb; break;
            default: throw new ArgumentOutOfRangeException(nameof(bandIndex), bandIndex, "Equalizer band index must be 0-9.");
        }
    }

    public void ApplyEqualizerPreset(SharpSelecta.Core.Audio.EqualizerPreset preset) =>
        _equalizer?.SetPreset(ToVendorPreset(preset));

    private static OwnaudioNET.Effects.EqualizerPreset ToVendorPreset(SharpSelecta.Core.Audio.EqualizerPreset preset) => preset switch
    {
        SharpSelecta.Core.Audio.EqualizerPreset.Default => OwnaudioNET.Effects.EqualizerPreset.Default,
        SharpSelecta.Core.Audio.EqualizerPreset.Bass => OwnaudioNET.Effects.EqualizerPreset.Bass,
        SharpSelecta.Core.Audio.EqualizerPreset.Treble => OwnaudioNET.Effects.EqualizerPreset.Treble,
        SharpSelecta.Core.Audio.EqualizerPreset.Rock => OwnaudioNET.Effects.EqualizerPreset.Rock,
        SharpSelecta.Core.Audio.EqualizerPreset.Classical => OwnaudioNET.Effects.EqualizerPreset.Classical,
        SharpSelecta.Core.Audio.EqualizerPreset.Pop => OwnaudioNET.Effects.EqualizerPreset.Pop,
        SharpSelecta.Core.Audio.EqualizerPreset.Jazz => OwnaudioNET.Effects.EqualizerPreset.Jazz,
        SharpSelecta.Core.Audio.EqualizerPreset.Voice => OwnaudioNET.Effects.EqualizerPreset.Voice,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

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
        _equalizer?.Dispose();
        _equalizer = null;
        _limiter?.Dispose();
        _limiter = null;

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
