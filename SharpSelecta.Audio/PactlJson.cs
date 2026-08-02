using System.Text.Json;

namespace SharpSelecta.Audio;

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
