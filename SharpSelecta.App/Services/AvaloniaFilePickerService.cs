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

    public async Task<string?> PickThemeFileAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ThemeFilePickerTitle,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Strings.ThemeFileTypeName) { Patterns = ["*.axaml"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickM3uImportFileAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ImportPlaylistFilePickerTitle,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Strings.PlaylistFileTypeName) { Patterns = ["*.m3u", "*.m3u8"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickM3uExportPathAsync(string suggestedFileName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.ExportPlaylistFilePickerTitle,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "m3u8",
            FileTypeChoices = [new FilePickerFileType(Strings.PlaylistFileTypeName) { Patterns = ["*.m3u8"] }],
        });

        return file?.TryGetLocalPath();
    }
}
