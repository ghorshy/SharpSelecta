namespace SharpSelecta.Core.Audio;

public interface IAudioEngine
{
    Task InitializeAsync();
    void Load(string filePath);
    void Play();
    void Pause();
}
