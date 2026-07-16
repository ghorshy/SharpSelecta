using System.Text.Json;

namespace SharpSelecta.Core.Library;

public static class LibrarySettingsStore
{
    public static string? LoadLibraryFolderPath(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize<LibrarySettingsData>(json)?.LibraryFolderPath;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public static void SaveLibraryFolderPath(string settingsFilePath, string folderPath)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(new LibrarySettingsData(folderPath)));
    }

    private sealed record LibrarySettingsData(string LibraryFolderPath);
}
