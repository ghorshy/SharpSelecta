using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class InterfaceSettingsViewModelTests
{
    // Each test gets its own directory (not just its own file name) so its Themes subfolder - computed from the
    // settings file's parent directory - never collides with another test's, whether run in parallel or not.
    private static string CreateTempSettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sharpselecta-interface-settings-vm-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "library-settings.json");
    }

    private static string ThemesDirectoryFor(string settingsPath) =>
        Path.Combine(Path.GetDirectoryName(settingsPath)!, "Themes");

    private static void DeleteTempSettingsDirectory(string settingsPath)
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static InterfaceSettingsViewModel CreateViewModel(string settingsPath, IFilePickerService? filePickerService = null) =>
        new(settingsPath, filePickerService ?? Substitute.For<IFilePickerService>());

    [Test]
    public async Task SelectedThemeDisplayName_DefaultsToDark()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
    }

    [Test]
    public async Task ThemeDisplayNames_ContainsSystemLightAndDark()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());

        await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
    }

    [Test]
    public async Task SettingSelectedThemeDisplayName_Persists()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(settingsPath);

            vm.SelectedThemeDisplayName = Strings.ThemeLight;

            await Assert.That(SettingsStore.LoadTheme(settingsPath)).IsEqualTo(AppTheme.Light);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
        }
    }

    [Test]
    public async Task Constructor_LoadsAPreviouslySavedTheme()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            SettingsStore.SaveTheme(settingsPath, AppTheme.System);

            var vm = CreateViewModel(settingsPath);

            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeSystem);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
        }
    }

    [Test]
    public async Task ImportTheme_WhenUserCancelsThePicker_DoesNothing()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var filePickerService = Substitute.For<IFilePickerService>();
            filePickerService.PickThemeFileAsync().Returns((string?)null);
            var vm = CreateViewModel(settingsPath, filePickerService);

            await vm.ImportThemeCommand.ExecuteAsync(null);

            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
            await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
        }
    }

    [Test]
    public async Task ImportTheme_WhenTheFileIsNotValidXaml_SetsAStatusMessageAndDoesNotAddIt()
    {
        var settingsPath = CreateTempSettingsPath();
        var invalidThemePath = Path.Combine(Path.GetTempPath(), $"sharpselecta-invalid-theme-{Guid.NewGuid():N}.axaml");
        try
        {
            File.WriteAllText(invalidThemePath, "not valid xaml at all {{{");
            var filePickerService = Substitute.For<IFilePickerService>();
            filePickerService.PickThemeFileAsync().Returns(invalidThemePath);
            var vm = CreateViewModel(settingsPath, filePickerService);

            await vm.ImportThemeCommand.ExecuteAsync(null);

            await Assert.That(vm.StatusMessage).IsEqualTo(Strings.InvalidThemeFile);
            await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
            File.Delete(invalidThemePath);
        }
    }

    [Test]
    public async Task ImportTheme_WithAValidResourceDictionary_AddsItAndSelectsIt()
    {
        var settingsPath = CreateTempSettingsPath();
        var themePath = Path.Combine(Path.GetTempPath(), $"MyCustomTheme-{Guid.NewGuid():N}.axaml");
        try
        {
            File.WriteAllText(themePath, ValidThemeXaml);
            var filePickerService = Substitute.For<IFilePickerService>();
            filePickerService.PickThemeFileAsync().Returns(themePath);
            var vm = CreateViewModel(settingsPath, filePickerService);

            await vm.ImportThemeCommand.ExecuteAsync(null);

            var expectedDisplayName = Path.GetFileNameWithoutExtension(themePath);
            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(expectedDisplayName);
            await Assert.That(vm.ThemeDisplayNames).Contains(expectedDisplayName);
            await Assert.That(SettingsStore.LoadTheme(settingsPath)).IsEqualTo(AppTheme.Custom);
            await Assert.That(File.Exists(Path.Combine(ThemesDirectoryFor(settingsPath), Path.GetFileName(themePath)))).IsTrue();
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
            File.Delete(themePath);
        }
    }

    [Test]
    public async Task RemoveCustomTheme_DeletesTheFileAndFallsBackToDark()
    {
        var settingsPath = CreateTempSettingsPath();
        var themePath = Path.Combine(Path.GetTempPath(), $"MyCustomTheme-{Guid.NewGuid():N}.axaml");
        try
        {
            File.WriteAllText(themePath, ValidThemeXaml);
            var filePickerService = Substitute.For<IFilePickerService>();
            filePickerService.PickThemeFileAsync().Returns(themePath);
            var vm = CreateViewModel(settingsPath, filePickerService);
            await vm.ImportThemeCommand.ExecuteAsync(null);
            var importedFileName = Path.GetFileName(themePath);

            vm.RemoveCustomThemeCommand.Execute(null);

            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
            await Assert.That(File.Exists(Path.Combine(ThemesDirectoryFor(settingsPath), importedFileName))).IsFalse();
            await Assert.That(SettingsStore.LoadTheme(settingsPath)).IsEqualTo(AppTheme.Dark);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
            File.Delete(themePath);
        }
    }

    [Test]
    public async Task RemoveCustomThemeCommand_WhenABuiltInThemeIsSelected_CannotExecute()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());

        await Assert.That(vm.RemoveCustomThemeCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task RefreshThemesCommand_PreservesTheCurrentSelection()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());
        vm.SelectedThemeDisplayName = Strings.ThemeLight;

        vm.RefreshThemesCommand.Execute(null);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeLight);
    }

    // Regression test: clearing ThemeDisplayNames mid-refresh nulls the bound ComboBox selection,
    // which used to crash with an ArgumentNullException from a null dictionary key lookup.
    [Test]
    public async Task SettingSelectedThemeDisplayName_ToNull_DoesNotThrow()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());
        vm.SelectedThemeDisplayName = Strings.ThemeLight;

        vm.SelectedThemeDisplayName = null!;

        await Assert.That(vm.SelectedThemeDisplayName).IsNull();
    }

    [Test]
    public async Task RemoveCustomThemeCommand_WhenSelectionIsTransientlyNull_CanExecuteWithoutThrowing()
    {
        var vm = CreateViewModel(CreateTempSettingsPath());
        vm.SelectedThemeDisplayName = null!;

        var canExecute = vm.RemoveCustomThemeCommand.CanExecute(null);

        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task Constructor_WhenTheSavedCustomThemeFileIsMissing_FallsBackToDark()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            SettingsStore.SaveTheme(settingsPath, AppTheme.Custom);
            SettingsStore.SaveCustomThemeFileName(settingsPath, "no-longer-there.axaml");

            var vm = CreateViewModel(settingsPath);

            await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
        }
        finally
        {
            DeleteTempSettingsDirectory(settingsPath);
        }
    }

    private const string ValidThemeXaml =
        """
        <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <SolidColorBrush x:Key="SystemAccentColor">#FF9D4EDD</SolidColorBrush>
        </ResourceDictionary>
        """;
}
