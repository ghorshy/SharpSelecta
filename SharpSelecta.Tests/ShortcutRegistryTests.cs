using System.Linq;
using SharpSelecta.App.Shortcuts;

namespace SharpSelecta.Tests;

public class ShortcutRegistryTests
{
    [Test]
    public async Task All_HasNoDuplicateIds()
    {
        await Assert.That(ShortcutRegistry.All.Select(s => s.Id).Distinct().Count()).IsEqualTo(ShortcutRegistry.All.Count);
    }

    [Test]
    public async Task All_HasNoDuplicateDefaultGestures()
    {
        await Assert.That(ShortcutRegistry.All.Select(s => s.DefaultGesture).Distinct().Count()).IsEqualTo(ShortcutRegistry.All.Count);
    }
}
