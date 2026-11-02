using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class InterfaceSettingsViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string ThemesDirectoryFor(string settingsPath) =>
        Path.Combine(Path.GetDirectoryName(settingsPath)!, "Themes");

    private static InterfaceSettingsViewModel CreateViewModel(string settingsPath, IFilePickerService? filePickerService = null) =>
        new(new SettingsStore(settingsPath), filePickerService ?? Substitute.For<IFilePickerService>());

    [Test]
    public async Task SelectedThemeDisplayName_DefaultsToDark()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
    }

    [Test]
    public async Task ThemeDisplayNames_ContainsSystemLightAndDark()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());

        await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
    }

    [Test]
    public async Task SettingSelectedThemeDisplayName_Persists()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(settingsPath);

        vm.SelectedThemeDisplayName = Strings.ThemeLight;

        await Assert.That(new SettingsStore(settingsPath).Current.Theme).IsEqualTo(AppTheme.Light);
    }

    [Test]
    public async Task Constructor_LoadsAPreviouslySavedTheme()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { Theme = AppTheme.System });

        var vm = CreateViewModel(settingsPath);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeSystem);
    }

    [Test]
    public async Task ImportTheme_WhenUserCancelsThePicker_DoesNothing()
    {
        var settingsPath = _temp.NewSettingsPath();
        var filePickerService = Substitute.For<IFilePickerService>();
        filePickerService.PickThemeFileAsync().Returns((string?)null);
        var vm = CreateViewModel(settingsPath, filePickerService);

        await vm.ImportThemeCommand.ExecuteAsync(null);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
        await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
    }

    [Test]
    public async Task ImportTheme_WhenTheFileIsNotValidXaml_SetsAStatusMessageAndDoesNotAddIt()
    {
        var settingsPath = _temp.NewSettingsPath();
        var invalidThemePath = _temp.FilePath("invalid-theme.axaml");
        File.WriteAllText(invalidThemePath, "not valid xaml at all {{{");
        var filePickerService = Substitute.For<IFilePickerService>();
        filePickerService.PickThemeFileAsync().Returns(invalidThemePath);
        var vm = CreateViewModel(settingsPath, filePickerService);

        await vm.ImportThemeCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsEqualTo(Strings.InvalidThemeFile);
        await Assert.That(vm.ThemeDisplayNames).IsEquivalentTo([Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark]);
    }

    [Test]
    public async Task ImportTheme_WithAValidResourceDictionary_AddsItAndSelectsIt()
    {
        var settingsPath = _temp.NewSettingsPath();
        var themePath = _temp.FilePath("MyCustomTheme.axaml");
        File.WriteAllText(themePath, ValidThemeXaml);
        var filePickerService = Substitute.For<IFilePickerService>();
        filePickerService.PickThemeFileAsync().Returns(themePath);
        var vm = CreateViewModel(settingsPath, filePickerService);

        await vm.ImportThemeCommand.ExecuteAsync(null);

        var expectedDisplayName = Path.GetFileNameWithoutExtension(themePath);
        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(expectedDisplayName);
        await Assert.That(vm.ThemeDisplayNames).Contains(expectedDisplayName);
        await Assert.That(new SettingsStore(settingsPath).Current.Theme).IsEqualTo(AppTheme.Custom);
        await Assert.That(File.Exists(Path.Combine(ThemesDirectoryFor(settingsPath), Path.GetFileName(themePath)))).IsTrue();
    }

    [Test]
    public async Task RemoveCustomTheme_DeletesTheFileAndFallsBackToDark()
    {
        var settingsPath = _temp.NewSettingsPath();
        var themePath = _temp.FilePath("MyCustomTheme.axaml");
        File.WriteAllText(themePath, ValidThemeXaml);
        var filePickerService = Substitute.For<IFilePickerService>();
        filePickerService.PickThemeFileAsync().Returns(themePath);
        var vm = CreateViewModel(settingsPath, filePickerService);
        await vm.ImportThemeCommand.ExecuteAsync(null);
        var importedFileName = Path.GetFileName(themePath);

        vm.RemoveCustomThemeCommand.Execute(null);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
        await Assert.That(File.Exists(Path.Combine(ThemesDirectoryFor(settingsPath), importedFileName))).IsFalse();
        await Assert.That(new SettingsStore(settingsPath).Current.Theme).IsEqualTo(AppTheme.Dark);
    }

    [Test]
    public async Task RemoveCustomThemeCommand_WhenABuiltInThemeIsSelected_CannotExecute()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());

        await Assert.That(vm.RemoveCustomThemeCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task RefreshThemesCommand_PreservesTheCurrentSelection()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());
        vm.SelectedThemeDisplayName = Strings.ThemeLight;

        vm.RefreshThemesCommand.Execute(null);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeLight);
    }

    // Regression test: clearing ThemeDisplayNames mid-refresh nulls the bound ComboBox selection,
    // which used to crash with an ArgumentNullException from a null dictionary key lookup.
    [Test]
    public async Task SettingSelectedThemeDisplayName_ToNull_DoesNotThrow()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());
        vm.SelectedThemeDisplayName = Strings.ThemeLight;

        vm.SelectedThemeDisplayName = null!;

        await Assert.That(vm.SelectedThemeDisplayName).IsNull();
    }

    [Test]
    public async Task RemoveCustomThemeCommand_WhenSelectionIsTransientlyNull_CanExecuteWithoutThrowing()
    {
        var vm = CreateViewModel(_temp.NewSettingsPath());
        vm.SelectedThemeDisplayName = null!;

        var canExecute = vm.RemoveCustomThemeCommand.CanExecute(null);

        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task Constructor_WhenTheSavedCustomThemeFileIsMissing_FallsBackToDark()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { Theme = AppTheme.Custom });
        new SettingsStore(settingsPath).Update(s => s with { CustomThemeFileName = "no-longer-there.axaml" });

        var vm = CreateViewModel(settingsPath);

        await Assert.That(vm.SelectedThemeDisplayName).IsEqualTo(Strings.ThemeDark);
    }

    private const string ValidThemeXaml =
        """
        <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <SolidColorBrush x:Key="SystemAccentColor">#FF9D4EDD</SolidColorBrush>
        </ResourceDictionary>
        """;
}
