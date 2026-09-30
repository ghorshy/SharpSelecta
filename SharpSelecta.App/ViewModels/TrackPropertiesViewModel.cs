using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class TrackPropertiesViewModel : ViewModelBase
{
    private const int MaxYearOrTrackNumber = 9999;

    private readonly IFilePickerService _filePickerService;
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger _logger;

    private TrackTagEdits _baseline;
    private bool _persistedHasCover;
    private bool _persistedCoverIsSeparateFile;
    private byte[]? _replacementCover;
    private bool _coverRemoved;
    private bool _isApplying;

    public Track Track { get; private set; }

    public event EventHandler<Track>? TrackSaved;

    public event EventHandler? CloseRequested;

    public TrackPropertiesViewModel(
        Track track,
        IFilePickerService filePickerService,
        IFileManagerService fileManagerService,
        ILogger logger)
    {
        Track = track;
        _filePickerService = filePickerService;
        _fileManagerService = fileManagerService;
        _logger = logger;

        _baseline = BaselineFrom(track);
        LoadFieldsFrom(track);

        var artwork = MusicLibraryScanner.LoadArtworkWithSource(track.FilePath);
        _persistedHasCover = artwork is not null;
        _persistedCoverIsSeparateFile = artwork?.IsSeparateFile ?? false;
        CoverBytes = artwork?.Bytes;
        SaveCoverAsSeparateFile = _persistedCoverIsSeparateFile;
        HasFolderCoverFile = MusicLibraryScanner.HasCoverFile(track.FilePath);
    }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Artist { get; set; }

    [ObservableProperty]
    public partial string? AlbumArtist { get; set; }

    [ObservableProperty]
    public partial string? Album { get; set; }

    [ObservableProperty]
    public partial string? Genre { get; set; }

    [ObservableProperty]
    public partial string? Comment { get; set; }

    [ObservableProperty]
    public partial string YearText { get; set; } = "";

    [ObservableProperty]
    public partial string TrackNumberText { get; set; } = "";

    [ObservableProperty]
    public partial byte[]? CoverBytes { get; set; }

    [ObservableProperty]
    public partial bool SaveCoverAsSeparateFile { get; set; }

    // True while the folder holds a cover.* file: removing the cover then deletes it too, which
    // also affects the album's other tracks - the view asks for confirmation first.
    [ObservableProperty]
    public partial bool HasFolderCoverFile { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public string DisplayName => Track.DisplayName;

    public string DurationDisplay => TrackFormatting.FormatDuration(Track.Duration);

    public string FileTypeDisplay => Track.FileType ?? string.Empty;

    public string BitrateDisplay => TrackFormatting.FormatBitrate(Track.Bitrate);

    public string SampleRateDisplay => TrackFormatting.FormatSampleRate(Track.SampleRate);

    public string DateAddedDisplay => Track.DateAddedUtc == default
        ? string.Empty
        : Track.DateAddedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string Location => Track.FilePath;

    public string ShowInFileManagerLabel => _fileManagerService.ActionLabel;

    public bool HasCover => CoverBytes is not null;

    public bool HasYearError => !TryParseOptionalNumber(YearText, out _);

    public bool HasTrackNumberError => !TryParseOptionalNumber(TrackNumberText, out _);

    public bool IsValid => !HasYearError && !HasTrackNumberError;

    public bool IsDirty => CurrentEdits() != _baseline || CurrentCoverEdit() is not null;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(CoverBytes):
                OnPropertyChanged(nameof(HasCover));
                RemoveCoverCommand.NotifyCanExecuteChanged();
                RefreshState();
                break;
            case nameof(Title) or nameof(Artist) or nameof(AlbumArtist) or nameof(Album) or nameof(Genre)
                or nameof(Comment) or nameof(YearText) or nameof(TrackNumberText) or nameof(SaveCoverAsSeparateFile):
                RefreshState();
                break;
        }
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasYearError));
        OnPropertyChanged(nameof(HasTrackNumberError));
        ApplyCommand.NotifyCanExecuteChanged();
        OkCommand.NotifyCanExecuteChanged();
    }

    private bool CanApply() => IsDirty && IsValid && !_isApplying;

    private bool CanOk() => IsValid && !_isApplying;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync() => await TryApplyAsync();

    [RelayCommand(CanExecute = nameof(CanOk))]
    private async Task OkAsync()
    {
        if (await TryApplyAsync())
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ChangeCoverAsync()
    {
        if (await _filePickerService.PickCoverImageAsync() is { } path)
        {
            await SetCoverFromFileAsync(path);
        }
    }

    // Shared by the picker and by dropping an image onto the cover.
    public async Task SetCoverFromFileAsync(string path)
    {
        byte[] bytes;
        try
        {
            bytes = await Task.Run(() => File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = Strings.FailedToLoadFile(ex.Message);
            return;
        }

        if (!TrackTagEditor.IsSupportedCoverImage(bytes))
        {
            ErrorMessage = Strings.UnsupportedCoverImage;
            return;
        }

        ErrorMessage = null;
        _replacementCover = bytes;
        _coverRemoved = false;
        CoverBytes = bytes;
    }

    [RelayCommand(CanExecute = nameof(HasCover))]
    private void RemoveCover()
    {
        _replacementCover = null;
        // Nothing on disk to remove when the cover being discarded was only ever picked this session.
        _coverRemoved = _persistedHasCover;
        CoverBytes = null;
    }

    [RelayCommand]
    private Task ShowInFileManagerAsync() => _fileManagerService.RevealInFileManagerAsync(Track.FilePath);

    private async Task<bool> TryApplyAsync()
    {
        if (!IsDirty)
            return true;

        _isApplying = true;
        ErrorMessage = null;
        RefreshState();

        var edits = CurrentEdits();
        var cover = CurrentCoverEdit();
        var dateAddedUtc = Track.DateAddedUtc;
        var filePath = Track.FilePath;
        try
        {
            var (updated, hasFolderCoverFile) = await Task.Run(() =>
            {
                TrackTagEditor.Write(filePath, edits, cover);
                var track = MusicLibraryScanner.ReadTrackIfExists(filePath)
                    ?? throw new FileNotFoundException("The file disappeared while saving.", filePath);
                return (track, MusicLibraryScanner.HasCoverFile(filePath));
            });

            Track = updated with { DateAddedUtc = dateAddedUtc };
            _baseline = BaselineFrom(Track);
            LoadFieldsFrom(Track);
            _replacementCover = null;
            _coverRemoved = false;
            _persistedHasCover = CoverBytes is not null;
            _persistedCoverIsSeparateFile = _persistedHasCover && SaveCoverAsSeparateFile;
            HasFolderCoverFile = hasFolderCoverFile;
            OnPropertyChanged(string.Empty);

            TrackSaved?.Invoke(this, Track);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tags for {FilePath}", filePath);
            ErrorMessage = Strings.FailedToSaveTags(ex.Message);
            return false;
        }
        finally
        {
            _isApplying = false;
            RefreshState();
        }
    }

    private void LoadFieldsFrom(Track track)
    {
        Title = track.Title;
        Artist = track.Artist;
        AlbumArtist = track.AlbumArtist;
        Album = track.Album;
        Genre = track.Genre;
        Comment = track.Comment;
        YearText = track.Year?.ToString(CultureInfo.InvariantCulture) ?? "";
        TrackNumberText = track.TrackNumber?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private static TrackTagEdits BaselineFrom(Track track) => new(
        Normalize(track.Title), Normalize(track.Artist), Normalize(track.AlbumArtist), Normalize(track.Album),
        Normalize(track.Genre), Normalize(track.Comment), track.Year, track.TrackNumber);

    private TrackTagEdits CurrentEdits()
    {
        // An unparseable number keeps its baseline value here: the field is flagged invalid and
        // Apply/OK are disabled, so this value is only ever used for the dirty comparison.
        var year = TryParseOptionalNumber(YearText, out var parsedYear) ? parsedYear : _baseline.Year;
        var trackNumber = TryParseOptionalNumber(TrackNumberText, out var parsedTrack) ? parsedTrack : _baseline.TrackNumber;
        return new TrackTagEdits(
            Normalize(Title), Normalize(Artist), Normalize(AlbumArtist), Normalize(Album),
            Normalize(Genre), Normalize(Comment), year, trackNumber);
    }

    private CoverArtEdit? CurrentCoverEdit()
    {
        if (_coverRemoved)
            return new CoverArtEdit.Remove();

        if (_replacementCover is { } replacement)
            return new CoverArtEdit.Replace(replacement, SaveCoverAsSeparateFile);

        // Same image, different storage: rewrite it in the newly chosen place.
        if (CoverBytes is { } existing && SaveCoverAsSeparateFile != _persistedCoverIsSeparateFile)
            return new CoverArtEdit.Replace(existing, SaveCoverAsSeparateFile);

        return null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseOptionalNumber(string? text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed is < 1 or > MaxYearOrTrackNumber)
            return false;

        value = parsed;
        return true;
    }
}
