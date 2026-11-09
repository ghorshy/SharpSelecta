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

    public static IReadOnlyList<long> ParseSinkIndexes(string sinksJson)
    {
        using var document = JsonDocument.Parse(sinksJson);
        return Indexes(document.RootElement.EnumerateArray());
    }

    public static IReadOnlyList<long> FindSinkInputIndexes(string sinkInputsJson, string processName)
    {
        using var document = JsonDocument.Parse(sinkInputsJson);
        return Indexes(OwnSinkInputs(document, processName));
    }

    public static long? FindOwnStreamSink(string sinkInputsJson, string processName)
    {
        using var document = JsonDocument.Parse(sinkInputsJson);
        foreach (var sinkInput in OwnSinkInputs(document, processName))
        {
            if (sinkInput.TryGetProperty("sink", out var sink))
            {
                return sink.GetInt64();
            }
        }

        return null;
    }

    private static List<long> Indexes(IEnumerable<JsonElement> elements) =>
        elements.Where(e => e.TryGetProperty("index", out _)).Select(e => e.GetProperty("index").GetInt64()).ToList();

    private static IEnumerable<JsonElement> OwnSinkInputs(JsonDocument document, string processName) =>
        document.RootElement.EnumerateArray().Where(sinkInput =>
            sinkInput.TryGetProperty("properties", out var properties) &&
            (PropertyContains(properties, "application.name", processName) || PropertyContains(properties, "node.name", processName)));

    private static bool PropertyContains(JsonElement properties, string property, string processName) =>
        properties.TryGetProperty(property, out var value) &&
        value.GetString() is { } text &&
        text.Contains(processName, StringComparison.Ordinal);
}
