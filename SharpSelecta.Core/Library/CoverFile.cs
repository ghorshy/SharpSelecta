namespace SharpSelecta.Core.Library;

internal static class CoverFile
{
    private static readonly string[] BaseNames = ["cover", "folder", "front"];
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];

    // BaseNames order is the priority order when a folder has several.
    public static string? Find(string trackFilePath) =>
        EnumerateAll(trackFilePath).FirstOrDefault();

    public static IEnumerable<string> EnumerateAll(string trackFilePath)
    {
        var directory = Path.GetDirectoryName(trackFilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                && BaseNames.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Array.FindIndex(BaseNames, n => n.Equals(Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    // The extension is chosen from the image's magic bytes, not from wherever it came from.
    public static bool TryGetExtension(byte[] imageBytes, out string extension)
    {
        extension = imageBytes switch
        {
            [0xFF, 0xD8, ..] => ".jpg",
            [0x89, 0x50, 0x4E, 0x47, ..] => ".png",
            _ => "",
        };
        return extension.Length > 0;
    }

    public static string ExtensionFor(byte[] imageBytes) => TryGetExtension(imageBytes, out var extension)
        ? extension
        : throw new ArgumentException("Cover art must be a JPEG or PNG image.", nameof(imageBytes));
}
