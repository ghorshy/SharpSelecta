namespace SharpSelecta.Core.Library;

internal static class SettingsSiblingFile
{
    public static string PathFor(string settingsFilePath, string suffix)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(settingsFilePath)}{suffix}";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }
}
