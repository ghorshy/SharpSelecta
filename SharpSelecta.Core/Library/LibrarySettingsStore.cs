using System.Text.Json;

namespace SharpSelecta.Core.Library;

public static class LibrarySettingsStore
{
    public static string? LoadLibraryFolderPath(string settingsFilePath) => Load(settingsFilePath)?.LibraryFolderPath;

    public static void SaveLibraryFolderPath(string settingsFilePath, string folderPath) =>
        Save(settingsFilePath, Default(settingsFilePath) with { LibraryFolderPath = folderPath });

    public static ColumnVisibility? LoadColumnVisibility(string settingsFilePath) => Load(settingsFilePath)?.Columns;

    public static void SaveColumnVisibility(string settingsFilePath, ColumnVisibility columns) =>
        Save(settingsFilePath, Default(settingsFilePath) with { Columns = columns });

    public static IReadOnlyList<string>? LoadColumnOrder(string settingsFilePath) => Load(settingsFilePath)?.ColumnOrder;

    public static void SaveColumnOrder(string settingsFilePath, IReadOnlyList<string> columnOrder) =>
        Save(settingsFilePath, Default(settingsFilePath) with { ColumnOrder = columnOrder });

    public static double? LoadRightColumnWidth(string settingsFilePath) => Load(settingsFilePath)?.RightColumnWidth;

    public static void SaveRightColumnWidth(string settingsFilePath, double width) =>
        Save(settingsFilePath, Default(settingsFilePath) with { RightColumnWidth = width });

    public static IReadOnlyDictionary<string, double>? LoadColumnWidths(string settingsFilePath) => Load(settingsFilePath)?.ColumnWidths;

    public static void SaveColumnWidths(string settingsFilePath, IReadOnlyDictionary<string, double> columnWidths) =>
        Save(settingsFilePath, Default(settingsFilePath) with { ColumnWidths = columnWidths });

    private static LibrarySettingsData Default(string settingsFilePath) =>
        Load(settingsFilePath) ?? new LibrarySettingsData(null, null, null, null, null);

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

    private sealed record LibrarySettingsData(
        string? LibraryFolderPath,
        ColumnVisibility? Columns,
        IReadOnlyList<string>? ColumnOrder,
        double? RightColumnWidth,
        IReadOnlyDictionary<string, double>? ColumnWidths);
}
