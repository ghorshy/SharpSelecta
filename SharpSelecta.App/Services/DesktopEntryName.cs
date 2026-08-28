using System;

namespace SharpSelecta.App.Services;

// Parses the unlocalized Name= line out of a freedesktop.org .desktop file's [Desktop Entry] group.
public static class DesktopEntryName
{
    public static string? ExtractName(string desktopFileContent)
    {
        var inDesktopEntryGroup = false;
        foreach (var rawLine in desktopFileContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inDesktopEntryGroup = line == "[Desktop Entry]";
                continue;
            }

            if (inDesktopEntryGroup && line.StartsWith("Name=", StringComparison.Ordinal))
            {
                return line["Name=".Length..];
            }
        }

        return null;
    }
}
