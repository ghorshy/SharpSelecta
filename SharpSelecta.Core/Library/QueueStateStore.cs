using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Core.Library;

public static partial class QueueStateStore
{
    public sealed record QueueEntryData(string FilePath, QueueEntrySource Source);

    public sealed record QueueState(IReadOnlyList<QueueEntryData> Entries, int CurrentIndex, double PositionSeconds);

    public static QueueState? Load(string settingsFilePath)
    {
        var data = LoadRaw(settingsFilePath);
        return data?.Entries is { Count: > 0 } entries
            ? new QueueState(entries, data.CurrentIndex ?? -1, data.PositionSeconds ?? 0)
            : null;
    }

    public static void Save(string settingsFilePath, IReadOnlyList<QueueEntryData> entries, int currentIndex, double positionSeconds)
    {
        var path = QueueStateFilePath(settingsFilePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new QueueStateData(entries, currentIndex, positionSeconds);
        File.WriteAllText(path, JsonSerializer.Serialize(data, QueueStateJsonContext.Default.QueueStateData));
    }

    private static string QueueStateFilePath(string settingsFilePath)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(settingsFilePath)}.queue-state.json";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }

    private static QueueStateData? LoadRaw(string settingsFilePath)
    {
        var path = QueueStateFilePath(settingsFilePath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, QueueStateJsonContext.Default.QueueStateData);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private sealed record QueueStateData(IReadOnlyList<QueueEntryData>? Entries, int? CurrentIndex, double? PositionSeconds);

    [JsonSerializable(typeof(QueueStateData))]
    private partial class QueueStateJsonContext : JsonSerializerContext
    {
    }
}
