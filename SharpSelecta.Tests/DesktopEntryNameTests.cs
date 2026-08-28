using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class DesktopEntryNameTests
{
    [Test]
    public async Task ExtractName_ReturnsTheNameFromTheDesktopEntryGroup()
    {
        const string content = """
            [Desktop Entry]
            Type=Application
            Name=Dolphin
            Exec=dolphin %u
            """;

        await Assert.That(DesktopEntryName.ExtractName(content)).IsEqualTo("Dolphin");
    }

    [Test]
    public async Task ExtractName_IgnoresNameLinesOutsideTheDesktopEntryGroup()
    {
        const string content = """
            [Desktop Action NewWindow]
            Name=New Window

            [Desktop Entry]
            Type=Application
            Name=Files
            """;

        await Assert.That(DesktopEntryName.ExtractName(content)).IsEqualTo("Files");
    }

    [Test]
    public async Task ExtractName_WithNoNameLine_ReturnsNull()
    {
        const string content = """
            [Desktop Entry]
            Type=Application
            Exec=somefilemanager %u
            """;

        await Assert.That(DesktopEntryName.ExtractName(content)).IsNull();
    }

    [Test]
    public async Task ExtractName_WithEmptyContent_ReturnsNull()
    {
        await Assert.That(DesktopEntryName.ExtractName("")).IsNull();
    }
}
