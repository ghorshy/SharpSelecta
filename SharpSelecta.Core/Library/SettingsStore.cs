using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SharpSelecta.Core.Library;

public sealed partial class SettingsStore
{
    private readonly Lock _lock = new();
    private bool _keepCorruptFileAside;

    // Never overwrite a file we couldn't read; it's read again on the next update.
    private bool _unreadable;

    public SettingsStore(string filePath)
    {
        FilePath = filePath;
        Current = Read();
    }

    public string FilePath { get; }

    public AppSettings Current { get; private set; }

    public void Update(Func<AppSettings, AppSettings> update)
    {
        lock (_lock)
        {
            if (_unreadable)
            {
                var reread = Read();
                if (!_unreadable)
                {
                    Current = reread;
                }
            }

            Current = update(Current);
            if (_unreadable)
            {
                return;
            }

            if (_keepCorruptFileAside)
            {
                File.Move(FilePath, FilePath + ".corrupt", overwrite: true);
                _keepCorruptFileAside = false;
            }

            Write(FilePath, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.AppSettings));
        }
    }

    private AppSettings Read()
    {
        _unreadable = false;
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        string text;
        try
        {
            text = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
            return new AppSettings();
        }

        // Missing or null values (older files wrote nulls) fall back to the record's defaults.
        var merged = JsonSerializer.SerializeToNode(new AppSettings(), SettingsJsonContext.Default.AppSettings)!.AsObject();
        try
        {
            var saved = JsonNode.Parse(text) as JsonObject ?? throw new JsonException("Settings file is not a JSON object.");
            foreach (var (name, value) in saved)
            {
                if (value is not null)
                {
                    merged[name] = value.DeepClone();
                }
            }

            return merged.Deserialize(SettingsJsonContext.Default.AppSettings)!;
        }
        catch (JsonException)
        {
            _keepCorruptFileAside = true;
            return new AppSettings();
        }
    }

    private static void Write(string filePath, string json)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AppSettings))]
    private partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
