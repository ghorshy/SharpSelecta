using SharpSelecta.App.Services;
using SharpSelecta.App.Shortcuts;

namespace SharpSelecta.Tests;

public class ShortcutSettingsServiceTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-shortcut-settings-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task GetEffectiveGesture_WithNoOverride_ReturnsTheRegistryDefault()
    {
        var service = new ShortcutSettingsService(CreateTempSettingsPath());
        var shortcut = ShortcutRegistry.All[0];

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo(shortcut.DefaultGesture);
    }

    [Test]
    public async Task SetOverride_ChangesTheEffectiveGesture()
    {
        var service = new ShortcutSettingsService(CreateTempSettingsPath());
        var shortcut = ShortcutRegistry.All[0];

        service.SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo("Ctrl+Shift+Alt+F");
    }

    [Test]
    public async Task SetOverride_PersistsAcrossInstances()
    {
        var settingsPath = CreateTempSettingsPath();
        var shortcut = ShortcutRegistry.All[0];
        new ShortcutSettingsService(settingsPath).SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        var reloaded = new ShortcutSettingsService(settingsPath);

        await Assert.That(reloaded.GetEffectiveGesture(shortcut.Id)).IsEqualTo("Ctrl+Shift+Alt+F");
    }

    [Test]
    public async Task ResetOverride_RevertsToTheRegistryDefault()
    {
        var service = new ShortcutSettingsService(CreateTempSettingsPath());
        var shortcut = ShortcutRegistry.All[0];
        service.SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        service.ResetOverride(shortcut.Id);

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo(shortcut.DefaultGesture);
    }

    [Test]
    public async Task SetOverride_RaisesShortcutsChanged()
    {
        var service = new ShortcutSettingsService(CreateTempSettingsPath());
        var raised = false;
        service.ShortcutsChanged += (_, _) => raised = true;

        service.SetOverride(ShortcutRegistry.All[0].Id, "Ctrl+Shift+Alt+F");

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task ResetOverride_WithNoExistingOverride_DoesNotRaiseShortcutsChanged()
    {
        var service = new ShortcutSettingsService(CreateTempSettingsPath());
        var raised = false;
        service.ShortcutsChanged += (_, _) => raised = true;

        service.ResetOverride(ShortcutRegistry.All[0].Id);

        await Assert.That(raised).IsFalse();
    }
}
