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
}
