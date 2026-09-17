using System.Threading.Tasks;

namespace SharpSelecta.App.Services;

public interface IFilePickerService
{
    Task<string?> PickLibraryFolderAsync();

    Task<string?> PickThemeFileAsync();

    Task<string?> PickM3uImportFileAsync();

    Task<string?> PickM3uExportPathAsync(string suggestedFileName);
}
