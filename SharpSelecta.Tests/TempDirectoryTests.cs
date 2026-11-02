using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TempDirectoryTests
{
    [Test]
    public async Task Dispose_RemovesSettingsFileAndItsSiblingFiles()
    {
        string settingsPath;
        using (var temp = new TempDirectory())
        {
            settingsPath = temp.NewSettingsPath();
            new SettingsStore(settingsPath).Update(s => s with { Volume = 0.5 });
            await File.WriteAllTextAsync(LibraryIndexStore.IndexFilePath(settingsPath), "");
        }

        await Assert.That(Directory.Exists(Path.GetDirectoryName(settingsPath))).IsFalse();
    }

    [Test]
    public async Task NewSettingsPath_ReturnsDistinctPathsInsideTheDirectory()
    {
        using var temp = new TempDirectory();

        var first = temp.NewSettingsPath();
        var second = temp.NewSettingsPath();

        await Assert.That(first).IsNotEqualTo(second);
        await Assert.That(Path.GetDirectoryName(first)).IsEqualTo(temp.Path);
    }
}
