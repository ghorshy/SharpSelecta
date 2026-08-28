using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpSelecta.App.Services;

// The freedesktop.org file-manager D-Bus interface - the standard cross-desktop way to ask
// whichever file manager is running to open a folder with specific files selected. Implemented by
// Nautilus, Thunar, and PCManFM-Qt among others (confirmed via Thunar's own D-Bus service file,
// which registers this well-known name alongside its legacy org.xfce.FileManager one).
// https://www.freedesktop.org/wiki/Specifications/file-manager-interface/
[DBusInterface("org.freedesktop.FileManager1")]
public interface IFileManager1 : IDBusObject
{
    Task ShowItemsAsync(string[] uris, string startupId);

    Task ShowFoldersAsync(string[] uris, string startupId);

    Task ShowItemPropertiesAsync(string[] uris, string startupId);
}
