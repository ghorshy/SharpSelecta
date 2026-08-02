using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

// Fallback path (Windows, or a Linux session without a Pulse-protocol server): device selection
// goes straight to the engine's own device enumeration and switch. Owns the off-UI-thread hop -
// both IAudioEngine calls block (SetOutputDevice is a full ~1s native Stop/switch/Start cycle)
// and callers are ViewModels on the UI thread.
public sealed class EngineOutputDeviceService(IAudioEngine audioEngine) : IOutputDeviceService
{
    public Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync() =>
        Task.Run(audioEngine.GetOutputDevices);

    public Task SetOutputDeviceAsync(string? deviceName) =>
        Task.Run(() => audioEngine.SetOutputDevice(deviceName));
}
