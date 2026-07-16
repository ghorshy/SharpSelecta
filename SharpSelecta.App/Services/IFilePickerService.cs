using System.Threading.Tasks;

namespace SharpSelecta.App.Services;

public interface IFilePickerService
{
    Task<string?> PickAudioFileAsync();
}
