using System.Diagnostics;
using System.Threading.Tasks;

namespace SharpSelecta.App.Services;

public interface IFileManagerShell
{
    Task<bool> TryShowViaFileManager1Async(string filePath);

    void Start(ProcessStartInfo startInfo);

    (string? Name, string? SelectCapableExecutable) DetectLinuxFileManager();
}
