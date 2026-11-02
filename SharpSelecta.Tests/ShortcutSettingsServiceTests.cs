using SharpSelecta.App.Services;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ShortcutSettingsServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Test]
    public async Task GetEffectiveGesture_WithNoOverride_ReturnsTheRegistryDefault()
    {
        var service = new ShortcutSettingsService(new SettingsStore(_temp.NewSettingsPath()));
        var shortcut = ShortcutRegistry.All[0];

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo(shortcut.DefaultGesture);
    }

    [Test]
    public async Task SetOverride_ChangesTheEffectiveGesture()
    {
        var service = new ShortcutSettingsService(new SettingsStore(_temp.NewSettingsPath()));
        var shortcut = ShortcutRegistry.All[0];

        service.SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo("Ctrl+Shift+Alt+F");
    }

    [Test]
    public async Task SetOverride_PersistsAcrossInstances()
    {
        var settingsPath = _temp.NewSettingsPath();
        var shortcut = ShortcutRegistry.All[0];
        new ShortcutSettingsService(new SettingsStore(settingsPath)).SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        var reloaded = new ShortcutSettingsService(new SettingsStore(settingsPath));

        await Assert.That(reloaded.GetEffectiveGesture(shortcut.Id)).IsEqualTo("Ctrl+Shift+Alt+F");
    }

    [Test]
    public async Task ResetOverride_RevertsToTheRegistryDefault()
    {
        var service = new ShortcutSettingsService(new SettingsStore(_temp.NewSettingsPath()));
        var shortcut = ShortcutRegistry.All[0];
        service.SetOverride(shortcut.Id, "Ctrl+Shift+Alt+F");

        service.ResetOverride(shortcut.Id);

        await Assert.That(service.GetEffectiveGesture(shortcut.Id)).IsEqualTo(shortcut.DefaultGesture);
    }

    [Test]
    public async Task SetOverride_RaisesShortcutsChanged()
    {
        var service = new ShortcutSettingsService(new SettingsStore(_temp.NewSettingsPath()));
        var raised = false;
        service.ShortcutsChanged += (_, _) => raised = true;

        service.SetOverride(ShortcutRegistry.All[0].Id, "Ctrl+Shift+Alt+F");

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task ResetOverride_WithNoExistingOverride_DoesNotRaiseShortcutsChanged()
    {
        var service = new ShortcutSettingsService(new SettingsStore(_temp.NewSettingsPath()));
        var raised = false;
        service.ShortcutsChanged += (_, _) => raised = true;

        service.ResetOverride(ShortcutRegistry.All[0].Id);

        await Assert.That(raised).IsFalse();
    }
}
