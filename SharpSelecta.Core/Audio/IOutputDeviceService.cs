namespace SharpSelecta.Core.Audio;

public interface IOutputDeviceService
{
    // Raised on a background thread when the device our stream plays on disappears (unplugged, Bluetooth dropped).
    event EventHandler? ActiveDeviceLost;

    Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync();

    Task SetOutputDeviceAsync(string? deviceName);
}
