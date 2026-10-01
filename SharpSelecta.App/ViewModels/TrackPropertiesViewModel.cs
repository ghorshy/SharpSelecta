using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.Core.AlbumArt;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// Edits one track or several at once. With several, a field whose value differs between the
// tracks starts empty (shown as "varies") and is only written if the user types something into it;
// every other field starts at the shared value and is written to all tracks.
public sealed partial class TrackPropertiesViewModel : ViewModelBase
{
    private const int MaxYearOrTrackNumber = 9999;

    private readonly IFilePickerService _filePickerService;
    private readonly IFileManagerService _fileManagerService;
    private readonly ILogger _logger;
    private readonly List<Track> _tracks;
    private readonly HashSet<string> _varyingFields = [];

    private readonly List<TrackCredits> _credits;
    private readonly IReadOnlyList<IAlbumArtProvider> _albumArtProviders;

    private TrackTagEdits _baseline = new(null, null, null, null, null, null, null, null);
    private TrackCredits _creditsBaseline = TrackCredits.None;
    private bool _persistedHasCover;
    private bool _persistedCoverIsSeparateFile;
    private byte[]? _replacementCover;
    private bool _coverRemoved;
    private bool _isApplying;

    public TrackPropertiesViewModel(
        Track track,
        IFilePickerService filePickerService,
        IFileManagerService fileManagerService,
        ILogger logger,
        IReadOnlyList<IAlbumArtProvider>? albumArtProviders = null)
        : this([track], filePickerService, fileManagerService, logger, albumArtProviders)
    {
    }

    public TrackPropertiesViewModel(
        IReadOnlyList<Track> tracks,
        IFilePickerService filePickerService,
        IFileManagerService fileManagerService,
        ILogger logger,
        IReadOnlyList<IAlbumArtProvider>? albumArtProviders = null)
    {
        if (tracks.Count == 0)
            throw new ArgumentException("At least one track is required.", nameof(tracks));

        _tracks = [.. tracks];
        _credits = [.. _tracks.Select(t => TrackCredits.Read(t.FilePath))];
        _filePickerService = filePickerService;
        _fileManagerService = fileManagerService;
        _logger = logger;
        _albumArtProviders = albumArtProviders ?? [];

        LoadFieldsFromTracks();
        ApplyCoverState(ReadCoverState(_tracks));
    }

    public IReadOnlyList<Track> Tracks => _tracks;

    public Track Track => _tracks[0];

    public bool IsMultiple => _tracks.Count > 1;

    // Raised once per save with every track that was written (re-read from disk).
    public event EventHandler<IReadOnlyList<Track>>? TracksSaved;

    public event EventHandler? CloseRequested;

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

    // Credits besides Artist: only edited through the credits window, so no text boxes bind to these.
    [ObservableProperty]
    public partial string? Remixer { get; set; }

    [ObservableProperty]
    public partial string? Composer { get; set; }

    [ObservableProperty]
    public partial string? Conductor { get; set; }

    [ObservableProperty]
    public partial string? Lyricist { get; set; }

    [ObservableProperty]
    public partial string YearText { get; set; } = "";

    [ObservableProperty]
    public partial string TrackNumberText { get; set; } = "";

    [ObservableProperty]
    public partial string DiscNumberText { get; set; } = "";

    [ObservableProperty]
    public partial byte[]? CoverBytes { get; set; }

    // The tracks don't all share the same cover (or only some have one), so none is shown.
    [ObservableProperty]
    public partial bool CoverVaries { get; set; }

    [ObservableProperty]
    public partial bool SaveCoverAsSeparateFile { get; set; }

    // True while a track's folder holds a cover.* file: removing the cover then deletes it too, which
    // also affects the album's other tracks - the view asks for confirmation first.
    [ObservableProperty]
    public partial bool HasFolderCoverFile { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public string? TitlePlaceholder => PlaceholderFor(nameof(Title));

    public string? ArtistPlaceholder => PlaceholderFor(nameof(Artist));

    public string? AlbumArtistPlaceholder => PlaceholderFor(nameof(AlbumArtist));

    public string? AlbumPlaceholder => PlaceholderFor(nameof(Album));

    public string? GenrePlaceholder => PlaceholderFor(nameof(Genre));

    public string? CommentPlaceholder => PlaceholderFor(nameof(Comment));

    public string? YearPlaceholder => PlaceholderFor(nameof(YearText));

    public string? TrackNumberPlaceholder => PlaceholderFor(nameof(TrackNumberText));

    public string? DiscNumberPlaceholder => PlaceholderFor(nameof(DiscNumberText));

    public string SelectionSummary => IsMultiple ? Strings.TracksSelected(_tracks.Count) : Track.DisplayName;

    public string DisplayName => SelectionSummary;

    public string DurationDisplay => TrackFormatting.FormatDuration(TimeSpan.FromTicks(_tracks.Sum(t => t.Duration.Ticks)));

    public string FileTypeDisplay => CommonOrVaries(t => t.FileType ?? string.Empty);

    public string BitrateDisplay => CommonOrVaries(t => TrackFormatting.FormatBitrate(t.Bitrate));

    public string SampleRateDisplay => CommonOrVaries(t => TrackFormatting.FormatSampleRate(t.SampleRate));

    public string DateAddedDisplay => CommonOrVaries(t => t.DateAddedUtc == default
        ? string.Empty
        : t.DateAddedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

    public string Location
    {
        get
        {
            if (!IsMultiple)
                return Track.FilePath;

            var folders = _tracks.Select(t => Path.GetDirectoryName(t.FilePath) ?? string.Empty).Distinct().ToList();
            return folders.Count == 1 ? folders[0] : Strings.MultipleLocations;
        }
    }

    public string ShowInFileManagerLabel => _fileManagerService.ActionLabel;

    public bool HasCover => CoverBytes is not null;

    // Looking a cover up online needs to know which album: the (shared) album and artist fields.
    public bool CanSearchCoverOnline => _albumArtProviders.Count > 0 && SearchQuery() is not null;

    public bool CanRemoveCover => HasCover || CoverVaries;

    public bool HasYearError => !TryParseOptionalNumber(YearText, out _);

    public bool HasTrackNumberError => !TryParseOptionalNumber(TrackNumberText, out _);

    public bool HasDiscNumberError => !TryParseOptionalNumber(DiscNumberText, out _);

    public bool IsValid => !HasYearError && !HasTrackNumberError && !HasDiscNumberError;

    public bool IsDirty => CurrentEdits() != _baseline || CurrentCredits() != _creditsBaseline || CurrentCoverEdit() is not null;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(CoverBytes) or nameof(CoverVaries):
                OnPropertyChanged(nameof(HasCover));
                OnPropertyChanged(nameof(CanRemoveCover));
                RemoveCoverCommand.NotifyCanExecuteChanged();
                RefreshState();
                break;
            case nameof(Artist) or nameof(AlbumArtist) or nameof(Album):
                OnPropertyChanged(nameof(CanSearchCoverOnline));
                RefreshState();
                break;
            case nameof(Title) or nameof(Genre)
                or nameof(Comment) or nameof(YearText) or nameof(TrackNumberText) or nameof(DiscNumberText) or nameof(SaveCoverAsSeparateFile)
                or nameof(Remixer) or nameof(Composer) or nameof(Conductor) or nameof(Lyricist):
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
        OnPropertyChanged(nameof(HasDiscNumberError));
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

    public AlbumArtSearchViewModel CreateAlbumArtSearch() =>
        new(SearchQuery() ?? throw new InvalidOperationException("No album to search for."), _albumArtProviders);

    // Prefers the album artist (a compilation's artist is "Various Artists" on the tracks but not here).
    private AlbumArtQuery? SearchQuery()
    {
        var album = Normalize(Album);
        var artist = Normalize(AlbumArtist) ?? ArtistList.Split(Artist).FirstOrDefault();
        return album is null || artist is null ? null : new AlbumArtQuery(artist, album);
    }

    // Sets the cover from image bytes that already exist in memory (e.g. one downloaded online).
    public void SetCover(byte[] bytes)
    {
        if (!TrackTagEditor.IsSupportedCoverImage(bytes))
        {
            ErrorMessage = Strings.UnsupportedCoverImage;
            return;
        }

        ErrorMessage = null;
        _replacementCover = bytes;
        _coverRemoved = false;
        CoverVaries = false;
        CoverBytes = bytes;
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

        SetCover(bytes);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCover))]
    private void RemoveCover()
    {
        _replacementCover = null;
        // Nothing on disk to remove when the cover being discarded was only ever picked this session.
        _coverRemoved = _persistedHasCover;
        CoverVaries = false;
        CoverBytes = null;
    }

    // Each role's field is its people joined by ATL's separator; the credits editor works on the list.
    public CreditsEditorViewModel CreateCreditsEditor() => new(
    [
        .. ArtistList.Split(Artist).Select(name => new CreditEntry(CreditRole.Artist, name)),
        .. ArtistList.Split(Remixer).Select(name => new CreditEntry(CreditRole.Remixer, name)),
        .. ArtistList.Split(Composer).Select(name => new CreditEntry(CreditRole.Composer, name)),
        .. ArtistList.Split(Conductor).Select(name => new CreditEntry(CreditRole.Conductor, name)),
        .. ArtistList.Split(Lyricist).Select(name => new CreditEntry(CreditRole.Lyricist, name)),
    ]);

    // A role that varied across the tracks and gets no entries stays null, i.e. untouched.
    public void ApplyCredits(IReadOnlyList<CreditEntry> credits)
    {
        string? PeopleIn(CreditRole role) => ArtistList.Join(credits.Where(c => c.Role == role).Select(c => c.Name));

        Artist = PeopleIn(CreditRole.Artist);
        Remixer = PeopleIn(CreditRole.Remixer);
        Composer = PeopleIn(CreditRole.Composer);
        Conductor = PeopleIn(CreditRole.Conductor);
        Lyricist = PeopleIn(CreditRole.Lyricist);
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

        var current = CurrentEdits();
        var cover = CurrentCoverEdit();
        // Resolved here, on the UI thread: the user can still type into the fields while saving.
        var typedCredits = CurrentCredits();
        var writeCredits = typedCredits != _creditsBaseline;
        var work = _tracks
            .Select((track, i) => (Track: track, Edits: EditsFor(track, current), Credits: writeCredits ? CreditsFor(_credits[i], typedCredits) : null))
            .ToList();
        try
        {
            var (results, coverState) = await Task.Run(() =>
            {
                var saved = new List<(Track Original, Track? Updated, Exception? Error, TrackCredits? Credits)>();
                foreach (var (track, edits, credits) in work)
                {
                    try
                    {
                        TrackTagEditor.Write(track.FilePath, edits, cover, credits);
                        var updated = MusicLibraryScanner.ReadTrackIfExists(track.FilePath)
                            ?? throw new FileNotFoundException("The file disappeared while saving.", track.FilePath);
                        saved.Add((track, updated with { DateAddedUtc = track.DateAddedUtc }, null, TrackCredits.Read(track.FilePath)));
                    }
                    catch (Exception ex)
                    {
                        saved.Add((track, null, ex, null));
                    }
                }

                var tracksNow = saved.Select(r => r.Updated ?? r.Original).ToList();
                return (saved, ReadCoverState(tracksNow));
            });

            var savedTracks = new List<Track>();
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i].Updated is not { } updated)
                    continue;

                _tracks[i] = updated;
                _credits[i] = results[i].Credits ?? _credits[i];
                savedTracks.Add(updated);
            }

            if (savedTracks.Count > 0)
            {
                TracksSaved?.Invoke(this, savedTracks);
            }

            var failures = results.Where(r => r.Error is not null).ToList();
            foreach (var failure in failures)
            {
                _logger.LogError(failure.Error, "Failed to save tags for {FilePath}", failure.Original.FilePath);
            }

            if (failures.Count > 0)
            {
                ErrorMessage = _tracks.Count == 1
                    ? Strings.FailedToSaveTags(failures[0].Error!.Message)
                    : Strings.FailedToSaveSomeTags(
                        results.Count - failures.Count,
                        results.Count,
                        string.Join(", ", failures.Select(f => Path.GetFileName(f.Original.FilePath))));
                OnPropertyChanged(string.Empty);
                return false;
            }

            // Everything is on disk: show what's really there now, which also resets "dirty".
            _replacementCover = null;
            _coverRemoved = false;
            LoadFieldsFromTracks();
            ApplyCoverState(coverState);
            OnPropertyChanged(string.Empty);
            return true;
        }
        finally
        {
            _isApplying = false;
            RefreshState();
        }
    }

    private void LoadFieldsFromTracks()
    {
        _varyingFields.Clear();
        Title = CommonText(nameof(Title), t => t.Title);
        Artist = CommonText(nameof(Artist), t => t.Artist);
        AlbumArtist = CommonText(nameof(AlbumArtist), t => t.AlbumArtist);
        Album = CommonText(nameof(Album), t => t.Album);
        Genre = CommonText(nameof(Genre), t => t.Genre);
        Comment = CommonText(nameof(Comment), t => t.Comment);
        YearText = CommonNumberText(nameof(YearText), t => t.Year);
        TrackNumberText = CommonNumberText(nameof(TrackNumberText), t => t.TrackNumber);
        DiscNumberText = CommonNumberText(nameof(DiscNumberText), t => t.DiscNumber);
        Remixer = CommonCredit(nameof(Remixer), c => c.Remixer);
        Composer = CommonCredit(nameof(Composer), c => c.Composer);
        Conductor = CommonCredit(nameof(Conductor), c => c.Conductor);
        Lyricist = CommonCredit(nameof(Lyricist), c => c.Lyricist);

        // Texts are valid here, so no baseline fallback is needed to read them back.
        _baseline = CurrentEdits();
        _creditsBaseline = CurrentCredits();
    }

    private string? CommonCredit(string field, Func<TrackCredits, string?> selector)
    {
        var values = _credits.Select(c => NormalizeList(selector(c))).Distinct().ToList();
        if (values.Count > 1)
        {
            _varyingFields.Add(field);
            return null;
        }

        return values[0];
    }

    private string? CommonText(string field, Func<Track, string?> selector)
    {
        var values = _tracks.Select(t => Normalize(selector(t))).Distinct().ToList();
        if (values.Count > 1)
        {
            _varyingFields.Add(field);
            return null;
        }

        return values[0];
    }

    private string CommonNumberText(string field, Func<Track, int?> selector)
    {
        var values = _tracks.Select(selector).Distinct().ToList();
        if (values.Count > 1)
        {
            _varyingFields.Add(field);
            return "";
        }

        return values[0]?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private string? PlaceholderFor(string field) => _varyingFields.Contains(field) ? Strings.Varies : null;

    private string CommonOrVaries(Func<Track, string> selector)
    {
        var values = _tracks.Select(selector).Distinct().ToList();
        return values.Count == 1 ? values[0] : Strings.Varies;
    }

    private void ApplyCoverState(CoverState state)
    {
        _persistedHasCover = state.AnyCover;
        _persistedCoverIsSeparateFile = state.AllSeparateFile;
        CoverVaries = state.Varies;
        CoverBytes = state.Bytes;
        SaveCoverAsSeparateFile = state.AllSeparateFile;
        HasFolderCoverFile = state.AnyFolderCoverFile;
    }

    private sealed record CoverState(byte[]? Bytes, bool Varies, bool AnyCover, bool AllSeparateFile, bool AnyFolderCoverFile);

    private static CoverState ReadCoverState(IReadOnlyList<Track> tracks)
    {
        var artworks = tracks.Select(t => MusicLibraryScanner.LoadArtworkWithSource(t.FilePath)).ToList();
        var anyFolderCover = tracks.Any(t => MusicLibraryScanner.HasCoverFile(t.FilePath));
        var anyCover = artworks.Any(a => a is not null);

        var first = artworks[0];
        var allSame = artworks.All(a => a is null == first is null
            && (a is null || (a.IsSeparateFile == first!.IsSeparateFile && a.Bytes.AsSpan().SequenceEqual(first.Bytes))));
        if (!allSame)
            return new CoverState(null, Varies: anyCover, anyCover, AllSeparateFile: false, anyFolderCover);

        return new CoverState(first?.Bytes, Varies: false, anyCover, AllSeparateFile: first?.IsSeparateFile ?? false, anyFolderCover);
    }

    private TrackTagEdits CurrentEdits()
    {
        // An unparseable number keeps its baseline value here: the field is flagged invalid and
        // Apply/OK are disabled, so this value is only ever used for the dirty comparison.
        var year = TryParseOptionalNumber(YearText, out var parsedYear) ? parsedYear : _baseline.Year;
        var trackNumber = TryParseOptionalNumber(TrackNumberText, out var parsedTrack) ? parsedTrack : _baseline.TrackNumber;
        var discNumber = TryParseOptionalNumber(DiscNumberText, out var parsedDisc) ? parsedDisc : _baseline.DiscNumber;
        return new TrackTagEdits(
            Normalize(Title), Normalize(Artist), Normalize(AlbumArtist), Normalize(Album),
            Normalize(Genre), Normalize(Comment), year, trackNumber, discNumber);
    }

    private TrackCredits CurrentCredits() =>
        new(NormalizeList(Remixer), NormalizeList(Composer), NormalizeList(Conductor), NormalizeList(Lyricist));

    // Same rule as EditsFor: a role that varied across the tracks and was left empty keeps this
    // track's own people.
    private TrackCredits CreditsFor(TrackCredits own, TrackCredits typed)
    {
        string? Pick(string field, string? value, string? ownValue) =>
            _varyingFields.Contains(field) && value is null ? NormalizeList(ownValue) : value;

        return new TrackCredits(
            Pick(nameof(Remixer), typed.Remixer, own.Remixer),
            Pick(nameof(Composer), typed.Composer, own.Composer),
            Pick(nameof(Conductor), typed.Conductor, own.Conductor),
            Pick(nameof(Lyricist), typed.Lyricist, own.Lyricist));
    }

    // What to write to one track: the typed value, except that a field that varied across the tracks
    // and was left empty keeps that track's own value.
    private TrackTagEdits EditsFor(Track track, TrackTagEdits typed)
    {
        string? Text(string field, string? value, string? own) =>
            _varyingFields.Contains(field) && value is null ? own : value;
        int? Number(string field, int? value, int? own) =>
            _varyingFields.Contains(field) && value is null ? own : value;

        return new TrackTagEdits(
            Text(nameof(Title), typed.Title, Normalize(track.Title)),
            Text(nameof(Artist), typed.Artist, Normalize(track.Artist)),
            Text(nameof(AlbumArtist), typed.AlbumArtist, Normalize(track.AlbumArtist)),
            Text(nameof(Album), typed.Album, Normalize(track.Album)),
            Text(nameof(Genre), typed.Genre, Normalize(track.Genre)),
            Text(nameof(Comment), typed.Comment, Normalize(track.Comment)),
            Number(nameof(YearText), typed.Year, track.Year),
            Number(nameof(TrackNumberText), typed.TrackNumber, track.TrackNumber),
            Number(nameof(DiscNumberText), typed.DiscNumber, track.DiscNumber));
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

    private static string? NormalizeList(string? value) => ArtistList.Join(ArtistList.Split(value));

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
