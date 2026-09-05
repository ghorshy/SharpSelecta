using System.Globalization;
using System.Resources;

namespace SharpSelecta.App.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("SharpSelecta.App.Resources.Strings", typeof(Strings).Assembly);

    public static string ChooseLibraryFolder => Get(nameof(ChooseLibraryFolder));
    public static string Play => Get(nameof(Play));
    public static string Pause => Get(nameof(Pause));
    public static string NoFileLoaded => Get(nameof(NoFileLoaded));
    public static string Previous => Get(nameof(Previous));
    public static string Next => Get(nameof(Next));
    public static string Volume => Get(nameof(Volume));
    public static string LibraryFolderPickerTitle => Get(nameof(LibraryFolderPickerTitle));
    public static string PlayNow => Get(nameof(PlayNow));
    public static string PlayNext => Get(nameof(PlayNext));
    public static string AddToQueue => Get(nameof(AddToQueue));
    public static string RemoveFromQueue => Get(nameof(RemoveFromQueue));
    public static string Queue => Get(nameof(Queue));
    public static string Clear => Get(nameof(Clear));
    public static string Playing => Get(nameof(Playing));
    public static string Settings => Get(nameof(Settings));
    public static string SettingsCategoryLibrary => Get(nameof(SettingsCategoryLibrary));
    public static string SettingsCategoryPlayback => Get(nameof(SettingsCategoryPlayback));
    public static string SettingsCategoryInterface => Get(nameof(SettingsCategoryInterface));
    public static string Theme => Get(nameof(Theme));
    public static string ThemeSystem => Get(nameof(ThemeSystem));
    public static string ThemeLight => Get(nameof(ThemeLight));
    public static string ThemeDark => Get(nameof(ThemeDark));
    public static string RestoreQueueOnStartup => Get(nameof(RestoreQueueOnStartup));
    public static string OutputDevice => Get(nameof(OutputDevice));
    public static string SystemDefaultAudioDevice => Get(nameof(SystemDefaultAudioDevice));
    public static string UseLogarithmicVolumeScale => Get(nameof(UseLogarithmicVolumeScale));
    public static string NoLibraryFoldersAdded => Get(nameof(NoLibraryFoldersAdded));
    public static string Ok => Get(nameof(Ok));
    public static string Apply => Get(nameof(Apply));
    public static string Cancel => Get(nameof(Cancel));
    public static string RepeatOff => Get(nameof(RepeatOff));
    public static string RepeatAll => Get(nameof(RepeatAll));
    public static string RepeatOne => Get(nameof(RepeatOne));
    public static string ColumnTrack => Get(nameof(ColumnTrack));
    public static string ColumnTitle => Get(nameof(ColumnTitle));
    public static string ColumnArtist => Get(nameof(ColumnArtist));
    public static string ColumnAlbum => Get(nameof(ColumnAlbum));
    public static string ColumnLength => Get(nameof(ColumnLength));
    public static string ColumnSampleRate => Get(nameof(ColumnSampleRate));
    public static string ColumnBitDepth => Get(nameof(ColumnBitDepth));
    public static string ColumnBitrate => Get(nameof(ColumnBitrate));
    public static string ColumnFileType => Get(nameof(ColumnFileType));
    public static string ColumnYear => Get(nameof(ColumnYear));
    public static string UnknownAlbum => Get(nameof(UnknownAlbum));
    public static string VariousArtists => Get(nameof(VariousArtists));
    public static string LoadingLibrary => Get(nameof(LoadingLibrary));
    public static string ClearArtworkCache => Get(nameof(ClearArtworkCache));
    public static string SortBy => Get(nameof(SortBy));
    public static string Search => Get(nameof(Search));
    public static string SettingsCategoryKeyboardShortcuts => Get(nameof(SettingsCategoryKeyboardShortcuts));
    public static string ShortcutSearchLibrary => Get(nameof(ShortcutSearchLibrary));
    public static string ShortcutIncreaseTileSize => Get(nameof(ShortcutIncreaseTileSize));
    public static string ShortcutDecreaseTileSize => Get(nameof(ShortcutDecreaseTileSize));
    public static string ShortcutSeekBackward => Get(nameof(ShortcutSeekBackward));
    public static string ShortcutSeekForward => Get(nameof(ShortcutSeekForward));
    public static string SeekStepSeconds => Get(nameof(SeekStepSeconds));
    public static string Reset => Get(nameof(Reset));
    public static string PressAKeyCombination => Get(nameof(PressAKeyCombination));
    public static string ShortcutNotSet => Get(nameof(ShortcutNotSet));
    public static string GenericFileManager => Get(nameof(GenericFileManager));

    public static string FailedToLoadFile(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToLoadFileFormat"), reason);

    public static string FailedToScanFolder(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToScanFolderFormat"), reason);

    public static string ShortcutConflict(string otherDescription) =>
        string.Format(CultureInfo.CurrentCulture, Get("ShortcutConflictFormat"), otherDescription);

    public static string ShowInFileManager(string name) =>
        string.Format(CultureInfo.CurrentCulture, Get("ShowInFileManagerFormat"), name);

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture)!;
}
