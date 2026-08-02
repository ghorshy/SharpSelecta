namespace SharpSelecta.Core.Audio;

public interface IOutputDeviceService
{
    Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync();

    Task SetOutputDeviceAsync(string? deviceName);
}
