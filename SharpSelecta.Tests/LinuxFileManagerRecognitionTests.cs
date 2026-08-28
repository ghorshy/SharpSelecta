using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class LinuxFileManagerRecognitionTests
{
    [Test]
    [Arguments("org.kde.dolphin.desktop", "dolphin")]
    [Arguments("dolphin.desktop", "dolphin")]
    [Arguments("org.gnome.Nautilus.desktop", "nautilus")]
    [Arguments("nautilus.desktop", "nautilus")]
    [Arguments("thunar.desktop", null)]
    [Arguments("thunar-settings.desktop", null)]
    [Arguments("pcmanfm.desktop", null)]
    [Arguments("pcmanfm-qt.desktop", null)]
    [Arguments("nemo.desktop", null)]
    [Arguments("io.elementary.files.desktop", null)]
    public async Task SelectCapableExecutable_RecognizesKnownFileManagers(string desktopFileId, string? expected)
    {
        await Assert.That(LinuxFileManagerRecognition.SelectCapableExecutable(desktopFileId)).IsEqualTo(expected);
    }
}
