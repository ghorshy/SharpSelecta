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
    public async Task Constructor_LoadsTheSavedAppearanceWithoutRewritingTheSettings()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with
        {
            FontSize = 18, AccentColor = "#D13438", TrackChangeNotifications = true,
        });
        var writtenAt = File.GetLastWriteTimeUtc(settingsPath);
        File.SetLastWriteTimeUtc(settingsPath, writtenAt.AddMinutes(-1));

        var vm = CreateViewModel(settingsPath);

        await Assert.That(vm.SelectedFontSizeOption.Size).IsEqualTo(18);
        await Assert.That(vm.AccentHex).IsEqualTo("#D13438");
        await Assert.That(vm.HasCustomAccent).IsTrue();
        await Assert.That(vm.ShowTrackChangeNotifications).IsTrue();
        await Assert.That(File.GetLastWriteTimeUtc(settingsPath)).IsEqualTo(writtenAt.AddMinutes(-1));
    }

    [Test]
    public async Task ChoosingTextSizeAndNotifications_Persists()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(settingsPath);

        vm.SelectedFontSizeOption = vm.FontSizeOptions.Single(o => o.Size == 16);
        vm.ShowTrackChangeNotifications = true;

        var saved = new SettingsStore(settingsPath).Current;
        await Assert.That(saved.FontSize).IsEqualTo(16);
        await Assert.That(saved.TrackChangeNotifications).IsTrue();

        vm.SelectedFontSizeOption = vm.FontSizeOptions[0];
        await Assert.That(new SettingsStore(settingsPath).Current.FontSize).IsNull();
    }

    [Test]
    public async Task ChoosingASwatch_SavesItsColorAndShowsItInTheHexFieldAndPicker()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(settingsPath);
        var red = AppearanceService.ParseAccent("#D13438")!.Value;

        vm.ChooseAccentSwatchCommand.Execute(new Avalonia.Media.Immutable.ImmutableSolidColorBrush(red));

        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsEqualTo("#D13438");
        await Assert.That(vm.AccentHex).IsEqualTo("#D13438");
        await Assert.That(vm.AccentPickerColor).IsEqualTo(red);
        await Assert.That(vm.UseThemeAccentCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task TypingAHexColor_SavesItOnlyOnceItIsComplete()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(settingsPath);

        vm.AccentHex = "#10";
        await Assert.That(vm.IsAccentHexInvalid).IsTrue();
        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsNull();

        vm.AccentHex = "#10893e";
        await Assert.That(vm.IsAccentHexInvalid).IsFalse();
        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsEqualTo("#10893E");
        await Assert.That(vm.AccentPickerColor).IsEqualTo(AppearanceService.ParseAccent("#10893E")!.Value);
    }

    [Test]
    public async Task PickingAColorInThePicker_SavesItWithoutAlpha()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(settingsPath);

        vm.AccentPickerColor = new Avalonia.Media.Color(0x80, 0x87, 0x64, 0xB8);

        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsEqualTo("#8764B8");
        await Assert.That(vm.AccentHex).IsEqualTo("#8764B8");
    }

    [Test]
    public async Task ThemeDefault_OrClearingTheHexField_DropsThePickedAccent()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { AccentColor = "#D13438" });
        var vm = CreateViewModel(settingsPath);

        vm.UseThemeAccentCommand.Execute(null);
        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsNull();
        await Assert.That(vm.AccentHex).IsEqualTo(string.Empty);
        await Assert.That(vm.UseThemeAccentCommand.CanExecute(null)).IsFalse();

        vm.AccentHex = "#D13438";
        vm.AccentHex = "";
        await Assert.That(new SettingsStore(settingsPath).Current.AccentColor).IsNull();
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
