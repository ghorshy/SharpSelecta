using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SharpSelecta.App.Services;

public sealed class AvaloniaFilePickerService(Window owner) : IFilePickerService
{
    public async Task<string?> PickAudioFileAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz plik muzyczny",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Pliki audio")
                {
                    Patterns = ["*.mp3", "*.flac", "*.wav", "*.m4a"],
                },
            ],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
