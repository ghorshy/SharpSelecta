namespace SharpSelecta.Core.Library;

// Paths handed to the app on the command line ("Open with" from a file manager).
public static class OpenedFiles
{
    public static IReadOnlyList<string> SupportedExtensions { get; } = [.. MusicLibraryScanner.SupportedExtensions, .. M3uPlaylistFile.Extensions];

    // A forwarded path is expanded by the running instance, whose working directory may differ.
    public static IReadOnlyList<string> ToAbsoluteArguments(IEnumerable<string> arguments, string workingDirectory)
    {
        var paths = new List<string>();
        foreach (var argument in arguments)
        {
            try
            {
                paths.Add(Path.GetFullPath(argument, workingDirectory));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return paths;
    }

    public static IReadOnlyList<string> Expand(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(MusicLibraryScanner.FindAudioFiles(path));
            }
            else if (IsPlaylist(path) && File.Exists(path))
            {
                files.AddRange(ReadPlaylist(path).Where(entry => MusicLibraryScanner.IsAudioFile(entry) && File.Exists(entry)));
            }
            else if (MusicLibraryScanner.IsAudioFile(path) && File.Exists(path))
            {
                files.Add(path);
            }
        }

        return files;
    }

    private static IReadOnlyList<string> ReadPlaylist(string path)
    {
        try
        {
            return M3uPlaylistFile.ParsePaths(File.ReadAllText(path), Path.GetDirectoryName(path) ?? "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsPlaylist(string path) =>
        M3uPlaylistFile.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
