using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class InterfaceSettingsViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-interface-settings-vm-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task SelectedThemeDisplayName_DefaultsToDark()
    {
        var vm = new InterfaceSettingsViewModel(CreateTempSettingsPath());

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
    }

    [Test]
    public async Task ThemeDisplayNames_ContainsSystemLightAndDark()
    {
        var vm = new InterfaceSettingsViewModel(CreateTempSettingsPath());

        await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
    }

    [Test]
    public async Task SettingSelectedThemeDisplayName_Persists()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = new InterfaceSettingsViewModel(settingsPath);

            vm.SelectedThemeDisplayName = Strings.ThemeLight;

            await Assert.That(SettingsStore.LoadTheme(settingsPath)).IsEqualTo(AppTheme.Light);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_LoadsAPreviouslySavedTheme()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            SettingsStore.SaveTheme(settingsPath, AppTheme.System);

            var vm = new InterfaceSettingsViewModel(settingsPath);

            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeSystem);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
