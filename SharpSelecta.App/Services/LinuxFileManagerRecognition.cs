using System;

namespace SharpSelecta.App.Services;

// Thunar and PCManFM are left out on purpose: their file selection doesn't work reliably.
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
