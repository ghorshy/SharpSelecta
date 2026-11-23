using System.Globalization;
using System.Resources;
using SharpSelecta.App.Shortcuts;

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
    public static string EqualizerPresetDefault => Get(nameof(EqualizerPresetDefault));
    public static string EqualizerPresetBass => Get(nameof(EqualizerPresetBass));
    public static string EqualizerPresetTreble => Get(nameof(EqualizerPresetTreble));
    public static string EqualizerPresetRock => Get(nameof(EqualizerPresetRock));
    public static string EqualizerPresetClassical => Get(nameof(EqualizerPresetClassical));
    public static string EqualizerPresetPop => Get(nameof(EqualizerPresetPop));
    public static string EqualizerPresetJazz => Get(nameof(EqualizerPresetJazz));
    public static string EqualizerPresetVoice => Get(nameof(EqualizerPresetVoice));
    public static string EqualizerPresetCustom => Get(nameof(EqualizerPresetCustom));
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
    public static string TextSize => Get(nameof(TextSize));
    public static string FontSizeThemeDefault => Get(nameof(FontSizeThemeDefault));
    public static string AccentColor => Get(nameof(AccentColor));
    public static string UseThemeAccent => Get(nameof(UseThemeAccent));
    public static string AccentHexPlaceholder => Get(nameof(AccentHexPlaceholder));
    public static string ShowTrackChangeNotifications => Get(nameof(ShowTrackChangeNotifications));
    public static string TrackChangeNotificationsHint => Get(nameof(TrackChangeNotificationsHint));
    public static string NotificationSkip => Get(nameof(NotificationSkip));
    public static string MinimizeToTray => Get(nameof(MinimizeToTray));
    public static string CloseToTray => Get(nameof(CloseToTray));
    public static string TrayHint => Get(nameof(TrayHint));
    public static string TrayShowWindow => Get(nameof(TrayShowWindow));
    public static string TrayQuit => Get(nameof(TrayQuit));
    public static string SettingsSectionFolders => Get(nameof(SettingsSectionFolders));
    public static string SettingsSectionMaintenance => Get(nameof(SettingsSectionMaintenance));
    public static string SettingsSectionStartup => Get(nameof(SettingsSectionStartup));
    public static string SettingsSectionPlayingTracks => Get(nameof(SettingsSectionPlayingTracks));
    public static string SettingsSectionOutput => Get(nameof(SettingsSectionOutput));
    public static string SettingsSectionVolume => Get(nameof(SettingsSectionVolume));
    public static string SettingsSectionSeeking => Get(nameof(SettingsSectionSeeking));
    public static string SettingsSectionAppearance => Get(nameof(SettingsSectionAppearance));
    public static string SettingsSectionNotifications => Get(nameof(SettingsSectionNotifications));
    public static string SettingsSectionTray => Get(nameof(SettingsSectionTray));
    public static string SettingsSectionSidePanel => Get(nameof(SettingsSectionSidePanel));
    public static string SidePanelPositionLabel => Get(nameof(SidePanelPositionLabel));
    public static string SidePanelRight => Get(nameof(SidePanelRight));
    public static string SidePanelLeft => Get(nameof(SidePanelLeft));
    public static string SidePanelHidden => Get(nameof(SidePanelHidden));
    public static string QueuePositionLabel => Get(nameof(QueuePositionLabel));
    public static string QueueOnTop => Get(nameof(QueueOnTop));
    public static string QueueAtTheBottom => Get(nameof(QueueAtTheBottom));
    public static string ShowNowPlayingArtwork => Get(nameof(ShowNowPlayingArtwork));
    public static string ShowNowPlayingDetails => Get(nameof(ShowNowPlayingDetails));
    public static string SettingsSectionDiscord => Get(nameof(SettingsSectionDiscord));
    public static string SettingsSectionAlbumArt => Get(nameof(SettingsSectionAlbumArt));
    public static string DiscordShowArtist => Get(nameof(DiscordShowArtist));
    public static string DiscordShowAlbum => Get(nameof(DiscordShowAlbum));
    public static string DiscordShowElapsed => Get(nameof(DiscordShowElapsed));
    public static string DiscordShowWhilePaused => Get(nameof(DiscordShowWhilePaused));
    public static string DiscordPaused => Get(nameof(DiscordPaused));

    public static string DiscordPausedArtists(string artists) =>
        string.Format(CultureInfo.CurrentCulture, Get("DiscordPausedArtistsFormat"), artists);

    public static string Pixels(int size) =>
        string.Format(CultureInfo.CurrentCulture, Get("PixelsFormat"), size);
    public static string RestoreQueueOnStartup => Get(nameof(RestoreQueueOnStartup));
    public static string ResumePlaybackOnLaunch => Get(nameof(ResumePlaybackOnLaunch));
    public static string AppName => Get(nameof(AppName));
    public static string AudioFileTypeName => Get(nameof(AudioFileTypeName));
    public static string OpenedFilesActionLabel => Get(nameof(OpenedFilesActionLabel));
    public static string OpenedFilesReplaceQueueAndPlay => Get(nameof(OpenedFilesReplaceQueueAndPlay));
    public static string OpenedFilesAddToQueue => Get(nameof(OpenedFilesAddToQueue));
    public static string OpenedFilesPlayNext => Get(nameof(OpenedFilesPlayNext));
    public static string TrackStartActionLabel => Get(nameof(TrackStartActionLabel));
    public static string TrackStartInsertAndPlay => Get(nameof(TrackStartInsertAndPlay));
    public static string TrackStartPlayFromHere => Get(nameof(TrackStartPlayFromHere));
    public static string TrackStartReplaceQueue => Get(nameof(TrackStartReplaceQueue));
    public static string TrackStartAddToQueue => Get(nameof(TrackStartAddToQueue));
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
    public static string FileTypeMp3 => Get(nameof(FileTypeMp3));
    public static string FileTypeFlac => Get(nameof(FileTypeFlac));
    public static string FileTypeWav => Get(nameof(FileTypeWav));
    public static string FileTypeM4a => Get(nameof(FileTypeM4a));
    public static string FileTypeM4aAlac => Get(nameof(FileTypeM4aAlac));
    public static string FileTypeM4aAac => Get(nameof(FileTypeM4aAac));
    public static string FileTypeOgg => Get(nameof(FileTypeOgg));
    public static string FileTypeAiff => Get(nameof(FileTypeAiff));
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
    public static string ShortcutSectionPlayback => Get(nameof(ShortcutSectionPlayback));
    public static string ShortcutSectionLibrary => Get(nameof(ShortcutSectionLibrary));
    public static string ShortcutSectionAlbumGrid => Get(nameof(ShortcutSectionAlbumGrid));
    public static string ShortcutSectionQueue => Get(nameof(ShortcutSectionQueue));
    public static string ShortcutSectionWindow => Get(nameof(ShortcutSectionWindow));
    public static string AlbumCoverWindowTitle => Get(nameof(AlbumCoverWindowTitle));
    public static string ShortcutToggleFullScreen => Get(nameof(ShortcutToggleFullScreen));
    public static string ShortcutSeekBackward => Get(nameof(ShortcutSeekBackward));
    public static string ShortcutSeekForward => Get(nameof(ShortcutSeekForward));
    public static string ShortcutPlayPause => Get(nameof(ShortcutPlayPause));
    public static string ShortcutPreviousTrack => Get(nameof(ShortcutPreviousTrack));
    public static string ShortcutNextTrack => Get(nameof(ShortcutNextTrack));
    public static string ShortcutClearQueue => Get(nameof(ShortcutClearQueue));
    public static string WatchLibraryFolders => Get(nameof(WatchLibraryFolders));
    public static string MetaSeparator => Get(nameof(MetaSeparator));
    public static string AlbumTrackCount(int count) => count == 1
        ? Get("AlbumOneTrack")
        : string.Format(CultureInfo.CurrentCulture, Get("AlbumTrackCountFormat"), count);
    public static string WatchLibraryFoldersHint => Get(nameof(WatchLibraryFoldersHint));
    public static string ShortcutVolumeUp => Get(nameof(ShortcutVolumeUp));
    public static string ShortcutVolumeDown => Get(nameof(ShortcutVolumeDown));
    public static string ShortcutToggleMute => Get(nameof(ShortcutToggleMute));
    public static string ShortcutToggleRepeat => Get(nameof(ShortcutToggleRepeat));
    public static string ShortcutToggleAutoDj => Get(nameof(ShortcutToggleAutoDj));
    public static string ShortcutRevealPlayingTrack => Get(nameof(ShortcutRevealPlayingTrack));
    public static string SettingsCategoryIntegrations => Get(nameof(SettingsCategoryIntegrations));
    public static string SettingsCategoryUpdates => Get(nameof(SettingsCategoryUpdates));
    public static string SettingsCategoryAbout => Get(nameof(SettingsCategoryAbout));
    public static string AboutTagline => Get(nameof(AboutTagline));
    public static string AboutCopyright => Get(nameof(AboutCopyright));
    public static string AboutLicense => Get(nameof(AboutLicense));
    public static string AboutCopyInfo => Get(nameof(AboutCopyInfo));
    public static string AboutCopied => Get(nameof(AboutCopied));
    public static string AboutSourceCode => Get(nameof(AboutSourceCode));
    public static string AboutReportBug => Get(nameof(AboutReportBug));
    public static string AboutOpenLogs => Get(nameof(AboutOpenLogs));
    public static string AboutLogsHint => Get(nameof(AboutLogsHint));
    public static string AboutLibraries => Get(nameof(AboutLibraries));
    public static string CheckForUpdatesAutomatically => Get(nameof(CheckForUpdatesAutomatically));
    public static string CheckForUpdatesHint => Get(nameof(CheckForUpdatesHint));
    public static string CheckForUpdatesNow => Get(nameof(CheckForUpdatesNow));
    public static string CheckingForUpdates => Get(nameof(CheckingForUpdates));
    public static string UpToDate => Get(nameof(UpToDate));
    public static string UpdateCheckFailed => Get(nameof(UpdateCheckFailed));
    public static string RestartToUpdate => Get(nameof(RestartToUpdate));
    public static string UpdateLater => Get(nameof(UpdateLater));
    public static string DiscordPresenceToggle => Get(nameof(DiscordPresenceToggle));
    public static string DiscordPresenceHint => Get(nameof(DiscordPresenceHint));
    public static string FanartApiKeyLabel => Get(nameof(FanartApiKeyLabel));
    public static string FanartApiKeyHint => Get(nameof(FanartApiKeyHint));
    public static string VolumeNormalizationLabel => Get(nameof(VolumeNormalizationLabel));
    public static string VolumeNormalizationOff => Get(nameof(VolumeNormalizationOff));
    public static string VolumeNormalizationTrack => Get(nameof(VolumeNormalizationTrack));
    public static string VolumeNormalizationAlbum => Get(nameof(VolumeNormalizationAlbum));
    public static string VolumeNormalizationHint => Get(nameof(VolumeNormalizationHint));
    public static string UntaggedGainLabel => Get(nameof(UntaggedGainLabel));

    public static string UntaggedGain(double gainDb) =>
        string.Format(CultureInfo.CurrentCulture, Get("UntaggedGainFormat"), gainDb);

    public static string AutoDj => Get(nameof(AutoDj));
    public static string AutoDjTooltip => Get(nameof(AutoDjTooltip));
    public static string ColumnDisc => Get(nameof(ColumnDisc));
    public static string ColumnAlbumArtist => Get(nameof(ColumnAlbumArtist));
    public static string ColumnGenre => Get(nameof(ColumnGenre));
    public static string LabelDiscNumber => Get(nameof(LabelDiscNumber));
    public static string ShortcutRefreshLibrary => Get(nameof(ShortcutRefreshLibrary));
    public static string ShortcutProperties => Get(nameof(ShortcutProperties));
    public static string SeekStepSeconds => Get(nameof(SeekStepSeconds));
    public static string PauseOnDeviceDisconnect => Get(nameof(PauseOnDeviceDisconnect));
    public static string PreviousRestartThresholdSeconds => Get(nameof(PreviousRestartThresholdSeconds));
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

    public static string ITunesCompilation => Get(nameof(ITunesCompilation));
    public static string FindInconsistentCompilationFlags => Get(nameof(FindInconsistentCompilationFlags));
    public static string CompilationFlagsWindowTitle => Get(nameof(CompilationFlagsWindowTitle));
    public static string CompilationFlagsHint => Get(nameof(CompilationFlagsHint));
    public static string CompilationFlagsChecking => Get(nameof(CompilationFlagsChecking));
    public static string CompilationFlagsNoneFound => Get(nameof(CompilationFlagsNoneFound));
    public static string CompilationFlagsWithoutFlag => Get(nameof(CompilationFlagsWithoutFlag));
    public static string ClearCompilationFlag => Get(nameof(ClearCompilationFlag));
    public static string Close => Get(nameof(Close));

    public static string CompilationFlagsAlbum(string album, string albumArtist) =>
        string.Format(CultureInfo.CurrentCulture, Get("CompilationFlagsAlbumFormat"), album, albumArtist);

    public static string FailedToCheckCompilationFlags(string message) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToCheckCompilationFlagsFormat"), message);

    public static string FailedToClearCompilationFlag(string failedNames) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToClearCompilationFlagFormat"), failedNames);

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

    public static string UpdateReady(string version) =>
        string.Format(CultureInfo.CurrentCulture, Get("UpdateReadyFormat"), version);

    public static string CurrentVersion(string version) =>
        string.Format(CultureInfo.CurrentCulture, Get("CurrentVersionFormat"), version);

    public static string BitrateKbps(int kbps) =>
        string.Format(CultureInfo.CurrentCulture, Get("BitrateKbpsFormat"), kbps);

    public static string FailedToSaveTags(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToSaveTagsFormat"), reason);

    public static string FailedToScanFolder(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToScanFolderFormat"), reason);

    public static string ShortcutScopeName(ShortcutScope scope) => scope switch
    {
        ShortcutScope.AlbumGrid => Get("ShortcutScopeAlbumGrid"),
        ShortcutScope.TrackList => Get("ShortcutScopeTrackList"),
        ShortcutScope.Queue => Get("ShortcutScopeQueue"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };

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

    public static string AboutSystem(string runtime, string os, string architecture) =>
        string.Format(CultureInfo.CurrentCulture, Get("AboutSystemFormat"), runtime, os, architecture);

    public static string SettingsSectionPlaybackBar => Get(nameof(SettingsSectionPlaybackBar));
    public static string PlaybackBarHint => Get(nameof(PlaybackBarHint));
    public static string PlaybackBarHidden => Get(nameof(PlaybackBarHidden));
    public static string PlaybackBarHiddenEmpty => Get(nameof(PlaybackBarHiddenEmpty));
    public static string PlayPause => Get(nameof(PlayPause));
    public static string ElapsedTime => Get(nameof(ElapsedTime));
    public static string ElapsedTileLabel => Get(nameof(ElapsedTileLabel));
    public static string SeekBar => Get(nameof(SeekBar));
    public static string TrackLength => Get(nameof(TrackLength));
    public static string LengthTileLabel => Get(nameof(LengthTileLabel));
    public static string Repeat => Get(nameof(Repeat));
    public static string SeekBarWidthLabel => Get(nameof(SeekBarWidthLabel));
    public static string SeekBarWidthFill => Get(nameof(SeekBarWidthFill));
    public static string SeekBarWidthFixed => Get(nameof(SeekBarWidthFixed));
    public static string PlaybackBarAlignment => Get(nameof(PlaybackBarAlignment));
    public static string AlignLeft => Get(nameof(AlignLeft));
    public static string AlignCenter => Get(nameof(AlignCenter));
    public static string AlignRight => Get(nameof(AlignRight));
    public static string ShowTrackTitleAboveBar => Get(nameof(ShowTrackTitleAboveBar));
    public static string TrackTitleAlignment => Get(nameof(TrackTitleAlignment));
    public static string CustomizeBar => Get(nameof(CustomizeBar));
    public static string RestoreDefaultLayout => Get(nameof(RestoreDefaultLayout));

    public static string Percent(int percent) =>
        string.Format(CultureInfo.CurrentCulture, Get("PercentFormat"), percent);

    public static string PlaybackBarLockedTile(string name) =>
        string.Format(CultureInfo.CurrentCulture, Get("PlaybackBarLockedTileFormat"), name);

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture)!;
}
