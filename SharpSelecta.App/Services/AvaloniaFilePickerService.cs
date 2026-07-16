using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.Services;

public sealed class AvaloniaFilePickerService(Window owner) : IFilePickerService
{
    public async Task<string?> PickLibraryFolderAsync()
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.LibraryFolderPickerTitle,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
