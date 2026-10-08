using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpSelecta.App.Services;

// https://www.freedesktop.org/wiki/Specifications/file-manager-interface/
[DBusInterface("org.freedesktop.FileManager1")]
public interface IFileManager1 : IDBusObject
{
    Task ShowItemsAsync(string[] uris, string startupId);

    Task ShowFoldersAsync(string[] uris, string startupId);

    Task ShowItemPropertiesAsync(string[] uris, string startupId);
}
