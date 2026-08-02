using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Core.Library;

// Split out of SettingsStore into its own sibling file: unlike every other setting, the queue is
// unbounded (grows with the queue) and session-shaped, so embedding it in the shared
// read-modify-write settings payload meant every unrelated settings save (a Ctrl+Scroll zoom
// notch, a column-width drag) paid a full serialize/deserialize of however large the last saved
// queue was.
public static partial class QueueStateStore
{
    public sealed record QueueEntryData(string FilePath, QueueEntrySource Source);

    public sealed record QueueState(IReadOnlyList<QueueEntryData> Entries, int CurrentIndex, double PositionSeconds);

    // Null when nothing was ever saved AND when the last saved queue was empty — both cases mean
    // "nothing to restore," so callers don't need to distinguish them.
    public static QueueState? Load(string settingsFilePath)
    {
        var data = LoadRaw(settingsFilePath);
        return data?.Entries is { Count: > 0 } entries
            ? new QueueState(entries, data.CurrentIndex ?? -1, data.PositionSeconds ?? 0)
            : null;
    }

    // Unlike SettingsStore's Save, this is a plain overwrite, not a read-modify-write — this file
    // holds nothing but queue state, so there's nothing else to preserve across a save.
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

    // A sibling of the main settings file, in the same directory, named after it - mirrors how
    // LibraryViewModel.ArtworkCacheDirectory derives its own sibling path from the same settings
    // file path, rather than threading a second path parameter through every constructor. Basing
    // the file name on the settings file's own name (not a fixed "queue-state.json") matters for
    // test isolation in particular: every test fixture's settings path already gets a unique GUID
    // in its file name, but several fixtures share the same directory (the OS temp folder) - a
    // fixed sibling name would collide across all of them.
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
