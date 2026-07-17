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
    public static string Playing => Get(nameof(Playing));
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

    public static string FailedToLoadFile(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToLoadFileFormat"), reason);

    public static string FailedToScanFolder(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Get("FailedToScanFolderFormat"), reason);

    private static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture)!;
}
