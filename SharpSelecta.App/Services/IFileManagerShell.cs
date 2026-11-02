using System.Diagnostics;

namespace SharpSelecta.App.Services;

public interface IFileManagerShell
{
    Task<bool> TryShowViaFileManager1Async(string filePath);

    void Start(ProcessStartInfo startInfo);

    (string? Name, string? SelectCapableExecutable) DetectLinuxFileManager();
}
