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
    public static string Equalizer => Get(nameof(Equalizer));
    public static string EqualizerEnabled => Get(nameof(EqualizerEnabled));
    public static string EqualizerPreset => Get(nameof(EqualizerPreset));
    public static string SettingsCategoryLibrary => Get(nameof(SettingsCategoryLibrary));
    public static string SettingsCategoryPlayback => Get(nameof(SettingsCategoryPlayback));
    public static string SettingsCategoryInterface => Get(nameof(SettingsCategoryInterface));
    public static string Theme => Get(nameof(Theme));
    public static string ThemeSystem => Get(nameof(ThemeSystem));
    public static string ThemeLight => Get(nameof(ThemeLight));
    public static string ThemeDark => Get(nameof(ThemeDark));
    public static string ImportThemeFile => Get(nameof(ImportThemeFile));
    public static string RemoveTheme => Get(nameof(RemoveTheme));
    public static string RefreshThemes => Get(nameof(RefreshThemes));
    public static string ThemeFilePickerTitle => Get(nameof(ThemeFilePickerTitle));
    public static string ThemeFileTypeName => Get(nameof(ThemeFileTypeName));
    public static string InvalidThemeFile => Get(nameof(InvalidThemeFile));
    public static string RestoreQueueOnStartup => Get(nameof(RestoreQueueOnStartup));
    public static string OutputDevice => Get(nameof(OutputDevice));
    public static string SystemDefaultAudioDevice => Get(nameof(SystemDefaultAudioDevice));
    public static string UseLogarithmicVolumeScale => Get(nameof(UseLogarithmicVolumeScale));
    public static string UseWaveformSlider => Get(nameof(UseWaveformSlider));
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
    public static string SortByDateAdded => Get(nameof(SortByDateAdded));
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
    public static string ShortcutPlayPause => Get(nameof(ShortcutPlayPause));
    public static string ShortcutPreviousTrack => Get(nameof(ShortcutPreviousTrack));
    public static string ShortcutNextTrack => Get(nameof(ShortcutNextTrack));
    public static string ShortcutClearQueue => Get(nameof(ShortcutClearQueue));
    public static string SettingsCategoryIntegrations => Get(nameof(SettingsCategoryIntegrations));
    public static string DiscordPresenceToggle => Get(nameof(DiscordPresenceToggle));
    public static string DiscordPresenceHint => Get(nameof(DiscordPresenceHint));
    public static string AutoDj => Get(nameof(AutoDj));
    public static string AutoDjTooltip => Get(nameof(AutoDjTooltip));
    public static string ColumnDisc => Get(nameof(ColumnDisc));
    public static string ColumnAlbumArtist => Get(nameof(ColumnAlbumArtist));
    public static string ColumnGenre => Get(nameof(ColumnGenre));
    public static string LabelDiscNumber => Get(nameof(LabelDiscNumber));
    public static string ShortcutRefreshLibrary => Get(nameof(ShortcutRefreshLibrary));
    public static string ShortcutProperties => Get(nameof(ShortcutProperties));
    public static string SeekStepSeconds => Get(nameof(SeekStepSeconds));
    public static string Reset => Get(nameof(Reset));
    public static string PressAKeyCombination => Get(nameof(PressAKeyCombination));
    public static string ShortcutNotSet => Get(nameof(ShortcutNotSet));
    public static string GenericFileManager => Get(nameof(GenericFileManager));
    public static string View => Get(nameof(View));
    public static string ViewModeList => Get(nameof(ViewModeList));
    public static string ViewModeCoverArt => Get(nameof(ViewModeCoverArt));
    public static string NavLibrary => Get(nameof(NavLibrary));
    public static string NavRecentlyAdded => Get(nameof(NavRecentlyAdded));
    public static string Playlists => Get(nameof(Playlists));
    public static string NewPlaylistEllipsis => Get(nameof(NewPlaylistEllipsis));
    public static string NewPlaylistPromptTitle => Get(nameof(NewPlaylistPromptTitle));
    public static string RenamePlaylistPromptTitle => Get(nameof(RenamePlaylistPromptTitle));
    public static string PlaylistNamePrompt => Get(nameof(PlaylistNamePrompt));
    public static string DeletePlaylistConfirmTitle => Get(nameof(DeletePlaylistConfirmTitle));
    public static string DeletePlaylistConfirmMessage => Get(nameof(DeletePlaylistConfirmMessage));
    public static string Rename => Get(nameof(Rename));
    public static string Delete => Get(nameof(Delete));
    public static string AddToPlaylist => Get(nameof(AddToPlaylist));
    public static string RemoveFromPlaylist => Get(nameof(RemoveFromPlaylist));
    public static string ImportFromM3u => Get(nameof(ImportFromM3u));
    public static string ExportToM3u => Get(nameof(ExportToM3u));
    public static string ImportPlaylistFilePickerTitle => Get(nameof(ImportPlaylistFilePickerTitle));
    public static string ExportPlaylistFilePickerTitle => Get(nameof(ExportPlaylistFilePickerTitle));
    public static string PlaylistFileTypeName => Get(nameof(PlaylistFileTypeName));
    public static string PlaylistImported => Get(nameof(PlaylistImported));

    public static string FailedToLoadFile(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToLoadFileFormat"), reason);

    public static string Properties => Get(nameof(Properties));
    public static string TabTagEditor => Get(nameof(TabTagEditor));
    public static string TabConvert => Get(nameof(TabConvert));
    public static string ConvertFromLabel => Get(nameof(ConvertFromLabel));
    public static string ConvertArrow => Get(nameof(ConvertArrow));
    public static string ConvertKindLossless => Get(nameof(ConvertKindLossless));
    public static string ConvertKindLossy => Get(nameof(ConvertKindLossy));
    public static string ConvertWavTagsHint => Get(nameof(ConvertWavTagsHint));
    public static string LabelConvertTo => Get(nameof(LabelConvertTo));
    public static string ConvertFormatMp3 => Get(nameof(ConvertFormatMp3));
    public static string ConvertFormatFlac => Get(nameof(ConvertFormatFlac));
    public static string ConvertFormatAlac => Get(nameof(ConvertFormatAlac));
    public static string ConvertFormatAac => Get(nameof(ConvertFormatAac));
    public static string ConvertFormatOgg => Get(nameof(ConvertFormatOgg));
    public static string ConvertFormatAiff => Get(nameof(ConvertFormatAiff));
    public static string ConvertFormatWav => Get(nameof(ConvertFormatWav));
    public static string KeepOriginalFile => Get(nameof(KeepOriginalFile));
    public static string OriginalWillBeTrashed => Get(nameof(OriginalWillBeTrashed));
    public static string ConvertButton => Get(nameof(ConvertButton));
    public static string ConverterUnavailable => Get(nameof(ConverterUnavailable));
    public static string ConvertReadingFiles => Get(nameof(ConvertReadingFiles));
    public static string ConvertBlockLossyToLossless => Get(nameof(ConvertBlockLossyToLossless));
    public static string ConvertBlockSameFormat => Get(nameof(ConvertBlockSameFormat));
    public static string ConvertBlockNoLowerBitrate => Get(nameof(ConvertBlockNoLowerBitrate));
    public static string ConvertBlockUnreadable => Get(nameof(ConvertBlockUnreadable));
    public static string ConvertBlockBitrateTooHigh => Get(nameof(ConvertBlockBitrateTooHigh));
    public static string ConvertCancelled => Get(nameof(ConvertCancelled));
    public static string LabelAlbumArtist => Get(nameof(LabelAlbumArtist));
    public static string LabelGenre => Get(nameof(LabelGenre));
    public static string LabelComments => Get(nameof(LabelComments));
    public static string LabelTrackNumber => Get(nameof(LabelTrackNumber));
    public static string LabelDuration => Get(nameof(LabelDuration));
    public static string LabelDateAdded => Get(nameof(LabelDateAdded));
    public static string LabelLocation => Get(nameof(LabelLocation));
    public static string LabelCover => Get(nameof(LabelCover));
    public static string CoverChooseImage => Get(nameof(CoverChooseImage));
    public static string CoverRemove => Get(nameof(CoverRemove));
    public static string CoverSaveAsSeparateFile => Get(nameof(CoverSaveAsSeparateFile));
    public static string CoverHint => Get(nameof(CoverHint));
    public static string RemoveCoverConfirmTitle => Get(nameof(RemoveCoverConfirmTitle));
    public static string RemoveCoverConfirmMessage => Get(nameof(RemoveCoverConfirmMessage));
    public static string CoverImagePickerTitle => Get(nameof(CoverImagePickerTitle));
    public static string CoverImageFileTypeName => Get(nameof(CoverImageFileTypeName));
    public static string UnsupportedCoverImage => Get(nameof(UnsupportedCoverImage));

    public static string CreditsEllipsis => Get(nameof(CreditsEllipsis));
    public static string CreditsEditorTitle => Get(nameof(CreditsEditorTitle));
    public static string CreditsEditorNewPlaceholder => Get(nameof(CreditsEditorNewPlaceholder));
    public static string Add => Get(nameof(Add));
    public static string Remove => Get(nameof(Remove));
    public static string AlbumArtFindOnline => Get(nameof(AlbumArtFindOnline));
    public static string AlbumArtWindowTitle => Get(nameof(AlbumArtWindowTitle));
    public static string AlbumArtSearching => Get(nameof(AlbumArtSearching));
    public static string AlbumArtNotFound => Get(nameof(AlbumArtNotFound));
    public static string AlbumArtFailed => Get(nameof(AlbumArtFailed));
    public static string AlbumArtNeedsKey => Get(nameof(AlbumArtNeedsKey));
    public static string AlbumArtEnterKey => Get(nameof(AlbumArtEnterKey));
    public static string AlbumArtKeyPromptTitle => Get(nameof(AlbumArtKeyPromptTitle));
    public static string AlbumArtKeyPrompt => Get(nameof(AlbumArtKeyPrompt));
    public static string AlbumArtNothingSelected => Get(nameof(AlbumArtNothingSelected));

    public static string AlbumArtQuery(string artist, string album) =>
        string.Format(CultureInfo.CurrentCulture, Get("AlbumArtQueryFormat"), artist, album);

    public static string RoleRemixer => Get(nameof(RoleRemixer));
    public static string RoleComposer => Get(nameof(RoleComposer));
    public static string RoleConductor => Get(nameof(RoleConductor));
    public static string RoleLyricist => Get(nameof(RoleLyricist));
    public static string ArtistSeparator => Get(nameof(ArtistSeparator));
    public static string Varies => Get(nameof(Varies));
    public static string MultipleLocations => Get(nameof(MultipleLocations));

    public static string TracksSelected(int count) =>
        string.Format(CultureInfo.CurrentCulture, Get("TracksSelectedFormat"), count);

    public static string FailedToSaveSomeTags(int saved, int total, string failedNames) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToSaveSomeTagsFormat"), saved, total, failedNames);

    public static string ConvertSkipped(int skipped, int total) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertSkippedFormat"), skipped, total);

    public static string ConvertedAll(int count) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertedAllFormat"), count);

    public static string ConvertedSome(int converted, int total, string failedNames) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertedSomeFormat"), converted, total, failedNames);

    public static string ConvertTrashFailed(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertTrashFailedFormat"), reason);

    public static string ConvertSourceKind(string format, string kind) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertSourceKindFormat"), format, kind);

    public static string ConvertGroup(int count, string label) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertGroupFormat"), count, label);

    public static string ConvertTrackCount(int count) =>
        string.Format(CultureInfo.CurrentCulture, Get("ConvertTrackCountFormat"), count);

    public static string BitrateKbps(int kbps) =>
        string.Format(CultureInfo.CurrentCulture, Get("BitrateKbpsFormat"), kbps);

    public static string FailedToSaveTags(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToSaveTagsFormat"), reason);

    public static string FailedToScanFolder(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToScanFolderFormat"), reason);

    public static string ShortcutConflict(string otherDescription) =>
        string.Format(CultureInfo.CurrentCulture, Get("ShortcutConflictFormat"), otherDescription);

    public static string ShowInFileManager(string name) =>
        string.Format(CultureInfo.CurrentCulture, Get("ShowInFileManagerFormat"), name);

    public static string FailedToImportTheme(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToImportThemeFormat"), reason);

    public static string FailedToRemoveTheme(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToRemoveThemeFormat"), reason);

    public static string CustomThemesFolder(string path) =>
        string.Format(CultureInfo.CurrentCulture, Get("CustomThemesFolderFormat"), path);

    public static string SkippedTracksNotInLibrary(int count) =>
        string.Format(CultureInfo.CurrentCulture, Get("SkippedTracksNotInLibraryFormat"), count);

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture)!;
}
