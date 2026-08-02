using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

// Routes device selection through the sound server instead of the engine's raw ALSA device
// list. Rationale: under PipeWire the engine's cpal/ALSA enumeration surfaces every PCM variant
// under its raw card name ("HDA ATI HDMI, HDMI 4", the FiiO E10 hiding as "DigiHug USB Audio")
// while the layer users actually see in pavucontrol - PipeWire sinks - has friendly names AND
// virtual sinks (EasyEffects) that plain ALSA can't target at all. The engine keeps playing to
// the system default; "switching device" here means moving this process's stream to another
// sink, which is instant (no ~1s native engine restart) and remembered per-application by
// WirePlumber across sessions.
//
// Everything goes through `pactl`, deliberately: it ships with pipewire-pulse (present on
// effectively every PipeWire desktop, and on plain PulseAudio ones too), and it is the only CLI
// with both JSON output and a stream-move command - wpctl has neither, and the raw
// pw-dump/pw-metadata route loses WirePlumber's remembered-routing behavior.
public sealed class PipeWireOutputDeviceService(ILogger<PipeWireOutputDeviceService> logger) : IOutputDeviceService
{
    // Resolves the user-facing selection back to the sink's stable name at call time. Sink
    // descriptions are what the picker displays and persists (matching pavucontrol), so two
    // identical DACs would collide on description - the first match wins, same as pavucontrol's
    // own list ordering.
    public async Task SetOutputDeviceAsync(string? deviceName)
    {
        string? targetSinkName;
        if (deviceName is null)
        {
            // pactl resolves this alias to whatever the current default sink is.
            targetSinkName = "@DEFAULT_SINK@";
        }
        else
        {
            var sinksJson = await RunPactlAsync("-f", "json", "list", "sinks");
            targetSinkName = sinksJson is null
                ? null
                : PactlJson.ParseSinks(sinksJson).FirstOrDefault(s => s.Description == deviceName).Name;
            if (targetSinkName is null)
            {
                logger.LogWarning("Output sink named {DeviceName} is no longer present - not moving the stream", deviceName);
                return;
            }
        }

        var sinkInputsJson = await RunPactlAsync("-f", "json", "list", "sink-inputs");
        if (sinkInputsJson is null)
        {
            return;
        }

        // The engine opens its output stream at initialization and keeps it open (the mixer runs
        // continuously), so under normal operation there is exactly one stream to move. Zero
        // matches means the engine isn't up (yet) - the saved preference still gets applied by
        // the next startup's ApplyPersistedOutputDeviceAsync pass.
        var processName = Process.GetCurrentProcess().ProcessName;
        var ownSinkInputs = PactlJson.FindSinkInputIndexes(sinkInputsJson, processName);
        if (ownSinkInputs.Count == 0)
        {
            logger.LogWarning("No live output stream found for this process - nothing to move to {DeviceName}", deviceName ?? "the default sink");
            return;
        }

        foreach (var sinkInputIndex in ownSinkInputs)
        {
            if (await RunPactlAsync("move-sink-input", sinkInputIndex.ToString(), targetSinkName) is not null)
            {
                logger.LogInformation("Moved output stream {SinkInput} to sink {DeviceName}", sinkInputIndex, deviceName ?? "system default");
            }
        }
    }

    public async Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync()
    {
        var sinksJson = await RunPactlAsync("-f", "json", "list", "sinks");
        if (sinksJson is null)
        {
            return [];
        }

        var defaultSinkName = (await RunPactlAsync("get-default-sink"))?.Trim();
        return PactlJson.ParseSinks(sinksJson)
            .Select(s => new AudioOutputDevice(s.Description, s.Name == defaultSinkName))
            .ToList();
    }

    // Composition-time probe (see AddAudioEngine): a session can have pactl on PATH without a
    // Pulse-protocol server behind it, so actually talk to the server rather than checking the
    // binary exists. Synchronous by design - it runs once during DI setup, before the window.
    public static bool IsAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("pactl", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            return process is not null && process.WaitForExit(2000) && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<string?> RunPactlAsync(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("pactl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                logger.LogWarning("pactl {Arguments} failed ({ExitCode}): {Error}", string.Join(' ', arguments), process.ExitCode, error.Trim());
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run pactl {Arguments}", string.Join(' ', arguments));
            return null;
        }
    }
}
