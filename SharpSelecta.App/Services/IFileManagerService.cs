using System.Threading.Tasks;

namespace SharpSelecta.App.Services;

public interface IFileManagerService
{
    // Ready-to-display menu text, e.g. "Show in Explorer" / "Show in Dolphin" / "Show in File Manager".
    string ActionLabel { get; }

    Task RevealInFileManagerAsync(string filePath);
}
