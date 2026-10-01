using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SharpSelecta.Core.Conversion;

namespace SharpSelecta.App.Services;

public sealed class FileTrashService : ITrashService
{
    public void MoveToTrash(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("The file does not exist.", path);

        if (OperatingSystem.IsWindows())
        {
            MoveToRecycleBin(path);
        }
        else if (!TryGioTrash(path))
        {
            MoveToFreedesktopTrash(path);
        }
    }

    private static bool TryGioTrash(string path)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("gio", ["trash", "--", path])
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    // The XDG trash spec's home trash: the file plus a .trashinfo recording where it came from. A file on
    // another filesystem can't be renamed into it, which is reported rather than copied.
    private static void MoveToFreedesktopTrash(string path)
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var filesDirectory = Path.Combine(dataHome, "Trash", "files");
        var infoDirectory = Path.Combine(dataHome, "Trash", "info");
        Directory.CreateDirectory(filesDirectory);
        Directory.CreateDirectory(infoDirectory);

        var name = Path.GetFileName(path);
        var target = Path.Combine(filesDirectory, name);
        for (var n = 1; File.Exists(target) || File.Exists(Path.Combine(infoDirectory, Path.GetFileName(target) + ".trashinfo")); n++)
        {
            target = Path.Combine(filesDirectory, $"{Path.GetFileNameWithoutExtension(name)} {n}{Path.GetExtension(name)}");
        }

        var infoPath = Path.Combine(infoDirectory, Path.GetFileName(target) + ".trashinfo");
        File.WriteAllText(infoPath,
            $"[Trash Info]\nPath={Uri.EscapeDataString(path).Replace("%2F", "/")}\nDeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}\n");
        try
        {
            File.Move(path, target);
        }
        catch
        {
            File.Delete(infoPath);
            throw;
        }
    }

    private const uint FoDelete = 0x3;
    private const ushort FofAllowUndo = 0x40;
    private const ushort FofNoConfirmation = 0x10;
    private const ushort FofSilent = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct operation);

    private static void MoveToRecycleBin(string path)
    {
        // The list is double-null-terminated.
        var operation = new ShFileOpStruct
        {
            Func = FoDelete,
            From = path + "\0\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent,
        };

        var result = SHFileOperationW(ref operation);
        if (result != 0 || operation.AnyOperationsAborted)
            throw new IOException($"The file could not be moved to the Recycle Bin (code {result}).");
    }
}
