using System.Text;

namespace SharpSelecta.Core.Library;

public static class M3uPlaylistFile
{
    public static IReadOnlyList<string> ParsePaths(string content, string baseDirectoryForRelativePaths)
    {
        var paths = new List<string>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            paths.Add(Path.IsPathRooted(line) ? line : Path.Combine(baseDirectoryForRelativePaths, line));
        }

        return paths;
    }

    public static string Write(IReadOnlyList<(string FilePath, TimeSpan Duration, string Artist, string Title)> tracks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");
        foreach (var (filePath, duration, artist, title) in tracks)
        {
            builder.AppendLine($"#EXTINF:{(int)duration.TotalSeconds},{artist} - {title}");
            builder.AppendLine(filePath);
        }

        return builder.ToString();
    }
}
