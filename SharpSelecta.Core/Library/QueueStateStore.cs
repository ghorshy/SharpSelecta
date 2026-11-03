using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Core.Library;

public static partial class QueueStateStore
{
    public sealed record QueueEntryData(string FilePath, QueueEntrySource Source);

    public sealed record QueueState(IReadOnlyList<QueueEntryData> Entries, int CurrentIndex, double PositionSeconds)
    {
        // Reads every file, so run it off the UI thread. The saved position only carries over when the
        // saved current file survives; otherwise the next survivor (or the last one) starts from the top.
        public ResolvedQueue Resolve()
        {
            var entries = new List<QueueEntry>();
            int? nextSurvivorIndex = null;
            var currentSurvived = false;
            for (var i = 0; i < Entries.Count; i++)
            {
                if (MusicLibraryScanner.ReadTrackIfExists(Entries[i].FilePath) is not { } track)
                    continue;

                var isAtOrPastSavedCurrent = CurrentIndex >= 0 && i >= CurrentIndex;
                if (isAtOrPastSavedCurrent && nextSurvivorIndex is null)
                {
                    nextSurvivorIndex = entries.Count;
                    currentSurvived = i == CurrentIndex;
                }

                entries.Add(new QueueEntry(track, Entries[i].Source));
            }

            if (entries.Count == 0)
                return new ResolvedQueue(entries, -1, 0);

            var currentIndex = CurrentIndex < 0 ? 0 : nextSurvivorIndex ?? entries.Count - 1;
            return new ResolvedQueue(entries, currentIndex, currentSurvived ? PositionSeconds : 0);
        }
    }

    public sealed record ResolvedQueue(IReadOnlyList<QueueEntry> Entries, int CurrentIndex, double PositionSeconds);

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

    public static string QueueStateFilePath(string settingsFilePath) =>
        SettingsSiblingFile.PathFor(settingsFilePath, ".queue-state.json");

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
