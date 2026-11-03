using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// Lists the app under "Open with" for audio files and playlists without taking over the user's defaults.
[SupportedOSPlatform("windows")]
public sealed class WindowsFileAssociations(RegistryKey classes, string exePath)
{
    private const string ProgId = "SharpSelecta.AudioFile";

    private string ApplicationKeyPath => $@"Applications\{Path.GetFileName(exePath)}";

    private string OpenCommand => $"\"{exePath}\" \"%1\"";

    public static WindowsFileAssociations ForCurrentUser(string exePath) =>
        new(Registry.CurrentUser.CreateSubKey(@"Software\Classes"), exePath);

    public void Register()
    {
        using (var progId = classes.CreateSubKey(ProgId))
        {
            progId.SetValue(null, Strings.AudioFileTypeName);
            using var icon = progId.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{exePath}\",0");
            using var command = progId.CreateSubKey(@"shell\open\command");
            command.SetValue(null, OpenCommand);
        }

        using (var application = classes.CreateSubKey(ApplicationKeyPath))
        {
            application.SetValue("FriendlyAppName", Strings.AppName);
            using var command = application.CreateSubKey(@"shell\open\command");
            command.SetValue(null, OpenCommand);
            using var supportedTypes = application.CreateSubKey("SupportedTypes");
            foreach (var extension in OpenedFiles.SupportedExtensions)
            {
                supportedTypes.SetValue(extension, string.Empty);
            }
        }

        foreach (var extension in OpenedFiles.SupportedExtensions)
        {
            using var openWith = classes.CreateSubKey($@"{extension}\OpenWithProgids");
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        NotifyShell();
    }

    public void Unregister()
    {
        classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        classes.DeleteSubKeyTree(ApplicationKeyPath, throwOnMissingSubKey: false);
        foreach (var extension in OpenedFiles.SupportedExtensions)
        {
            using var openWith = classes.OpenSubKey($@"{extension}\OpenWithProgids", writable: true);
            openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
        }

        NotifyShell();
    }

    private static void NotifyShell() => SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);

    private const int ShcneAssocChanged = 0x08000000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
