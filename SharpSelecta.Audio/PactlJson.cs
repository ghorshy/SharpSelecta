using System.Text.Json;

namespace SharpSelecta.Audio;

// Pure parsers for `pactl -f json` output, split from PipeWireOutputDeviceService so they're
// testable against captured JSON without spawning processes. JsonDocument (not serialization)
// keeps this reflection-free.
public static class PactlJson
{
    public static IReadOnlyList<(string Name, string Description)> ParseSinks(string sinksJson)
    {
        using var document = JsonDocument.Parse(sinksJson);
        var sinks = new List<(string, string)>();
        foreach (var sink in document.RootElement.EnumerateArray())
        {
            if (sink.TryGetProperty("name", out var name) && name.GetString() is { } sinkName &&
                sink.TryGetProperty("description", out var description) && description.GetString() is { } sinkDescription)
            {
                sinks.Add((sinkName, sinkDescription));
            }
        }

        return sinks;
    }

    // Matches this process's playback streams by process NAME, not PID - confirmed against a
    // real PipeWire session that OwnAudioSharp's cpal/ALSA output on Linux never appears as a
    // native pipewire-pulse client (which would carry "application.process.id"). It surfaces
    // through PipeWire's ALSA compatibility bridge instead, named "PipeWire ALSA [ProcessName]"
    // (application.name) / "alsa_playback.ProcessName" (node.name), with no process-id property
    // at all. Checking both properties is resilience against either one changing format in a
    // future pipewire-alsa version.
    public static IReadOnlyList<long> FindSinkInputIndexes(string sinkInputsJson, string processName)
    {
        using var document = JsonDocument.Parse(sinkInputsJson);
        var indexes = new List<long>();
        foreach (var sinkInput in document.RootElement.EnumerateArray())
        {
            if (!sinkInput.TryGetProperty("index", out var index) || !sinkInput.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var applicationName = properties.TryGetProperty("application.name", out var appNameProp) ? appNameProp.GetString() : null;
            var nodeName = properties.TryGetProperty("node.name", out var nodeNameProp) ? nodeNameProp.GetString() : null;
            if ((applicationName is not null && applicationName.Contains(processName, StringComparison.Ordinal)) ||
                (nodeName is not null && nodeName.Contains(processName, StringComparison.Ordinal)))
            {
                indexes.Add(index.GetInt64());
            }
        }

        return indexes;
    }
}
