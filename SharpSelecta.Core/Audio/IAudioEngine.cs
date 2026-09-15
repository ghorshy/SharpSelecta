namespace SharpSelecta.Core.Audio;

public interface IAudioEngine : IDisposable
{
    Task InitializeAsync();
    void Load(string filePath);
    void Play();
    void Pause();
    void Seek(double positionSeconds);
    double Position { get; }
    double Duration { get; }
    float Volume { get; set; }

    IReadOnlyList<AudioOutputDevice> GetOutputDevices();

    // Null selects the system default device. Requires InitializeAsync to have completed.
    void SetOutputDevice(string? deviceName);

    bool EqualizerEnabled { get; set; }

    // Standard ISO 10-band graphic-EQ center frequencies, in Hz — display labels only; they do not affect gain routing.
    IReadOnlyList<int> EqualizerBandFrequenciesHz { get; }

    IReadOnlyList<float> EqualizerBandGainsDb { get; }

    void SetEqualizerBandGain(int bandIndex, float gainDb);

    void ApplyEqualizerPreset(EqualizerPreset preset);

    // One extra decoder pass over the loaded track; empty when nothing is loaded.
    // `points` is the number of buckets returned (may come back shorter for a duration-less container).
    IReadOnlyList<float> GetWaveformPeaks(int points);
}
