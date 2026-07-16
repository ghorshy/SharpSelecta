using System.Text.Json;

namespace SharpSelecta.Core.Library;

public static class LibrarySettingsStore
{
    public static string? LoadLibraryFolderPath(string settingsFilePath) => Load(settingsFilePath)?.LibraryFolderPath;

    public static void SaveLibraryFolderPath(string settingsFilePath, string folderPath) =>
        Save(settingsFilePath, (Load(settingsFilePath) ?? new LibrarySettingsData(null, null, null)) with { LibraryFolderPath = folderPath });

    public static ColumnVisibility? LoadColumnVisibility(string settingsFilePath) => Load(settingsFilePath)?.Columns;

    public static void SaveColumnVisibility(string settingsFilePath, ColumnVisibility columns) =>
        Save(settingsFilePath, (Load(settingsFilePath) ?? new LibrarySettingsData(null, null, null)) with { Columns = columns });

    public static IReadOnlyList<string>? LoadColumnOrder(string settingsFilePath) => Load(settingsFilePath)?.ColumnOrder;

    public static void SaveColumnOrder(string settingsFilePath, IReadOnlyList<string> columnOrder) =>
        Save(settingsFilePath, (Load(settingsFilePath) ?? new LibrarySettingsData(null, null, null)) with { ColumnOrder = columnOrder });

    private static LibrarySettingsData? Load(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize<LibrarySettingsData>(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static void Save(string settingsFilePath, LibrarySettingsData data)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(data));
    }

    private sealed record LibrarySettingsData(string? LibraryFolderPath, ColumnVisibility? Columns, IReadOnlyList<string>? ColumnOrder);
}
