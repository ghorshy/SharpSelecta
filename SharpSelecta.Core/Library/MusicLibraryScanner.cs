namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    private static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a"];

    public static IReadOnlyList<Track> Scan(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new Track(path, Path.GetFileName(path)))
            .ToList();
    }
}
