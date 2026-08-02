namespace SharpSelecta.Core.Audio;

// Seam between the Settings device picker and whatever owns output routing on the current
// platform - the sound server (PipeWire/PulseAudio) when one is available, the engine's own
// device switching otherwise. Interface-worthy per the mock-vs-real rule: every implementation
// talks to external processes or native audio hardware. Device names double as identifiers;
// null means "system default".
public interface IOutputDeviceService
{
    Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync();

    Task SetOutputDeviceAsync(string? deviceName);
}
