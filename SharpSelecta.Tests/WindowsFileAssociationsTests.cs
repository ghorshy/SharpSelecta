using System.Runtime.Versioning;
using Microsoft.Win32;
using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class WindowsFileAssociationsTests
{
    private const string ExePath = @"C:\Users\me\AppData\Local\SharpSelecta\current\SharpSelecta.App.exe";

    [Test]
    [SupportedOSPlatform("windows")]
    public async Task Register_OffersTheAppForAudioFilesAndPlaylists_AndUnregisterRemovesIt()
    {
        Skip.When(!OperatingSystem.IsWindows(), "Windows registry");
        var testKeyPath = $@"Software\SharpSelectaTests\{Guid.NewGuid():N}";
        using var classes = Registry.CurrentUser.CreateSubKey(testKeyPath);
        try
        {
            var associations = new WindowsFileAssociations(classes, ExePath);

            associations.Register();

            await Assert.That(classes.OpenSubKey(@"SharpSelecta.AudioFile\shell\open\command")?.GetValue(null))
                .IsEqualTo($"\"{ExePath}\" \"%1\"");
            await Assert.That(classes.OpenSubKey(@".flac\OpenWithProgids")?.GetValueNames()).Contains("SharpSelecta.AudioFile");
            await Assert.That(classes.OpenSubKey(@".m3u\OpenWithProgids")?.GetValueNames()).Contains("SharpSelecta.AudioFile");
            await Assert.That(classes.OpenSubKey(@".txt\OpenWithProgids")).IsNull();

            associations.Unregister();

            await Assert.That(classes.OpenSubKey("SharpSelecta.AudioFile")).IsNull();
            await Assert.That(classes.OpenSubKey(@".flac\OpenWithProgids")?.GetValueNames() ?? []).DoesNotContain("SharpSelecta.AudioFile");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(testKeyPath, throwOnMissingSubKey: false);
        }
    }
}
