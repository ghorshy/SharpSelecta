using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class EngineOutputDeviceService(IAudioEngine audioEngine) : IOutputDeviceService
{
    public Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync() =>
        Task.Run(audioEngine.GetOutputDevices);

    public Task SetOutputDeviceAsync(string? deviceName) =>
        Task.Run(() => audioEngine.SetOutputDevice(deviceName));
}
