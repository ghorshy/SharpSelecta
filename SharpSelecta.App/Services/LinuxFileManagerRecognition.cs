using System;

namespace SharpSelecta.App.Services;

// Recognizes the few Linux file managers whose CLI can select a specific file (not just open its
// folder) from the desktop-file-id xdg-mime reports as the default handler for inode/directory.
// Thunar and PCManFM don't support this reliably even via their own D-Bus/CLI surface (Thunar's
// DisplayFolderAndSelect is a known no-op in practice), so they're deliberately left unrecognized -
// FileManagerService falls back to just opening the containing folder for those.
public static class LinuxFileManagerRecognition
{
    public static string? SelectCapableExecutable(string desktopFileId)
    {
        if (desktopFileId.Contains("dolphin", StringComparison.OrdinalIgnoreCase))
            return "dolphin";

        if (desktopFileId.Contains("nautilus", StringComparison.OrdinalIgnoreCase))
            return "nautilus";

        return null;
    }
}
