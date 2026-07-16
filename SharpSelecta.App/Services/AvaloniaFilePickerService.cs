using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Services;

public sealed class AvaloniaFilePickerService(Window owner) : IFilePickerService
{
    public async Task<string?> PickAudioFileAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.FilePickerTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.AudioFilesFilterName)
                {
                    Patterns = ["*.mp3", "*.flac", "*.wav", "*.m4a"],
                },
            ],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
