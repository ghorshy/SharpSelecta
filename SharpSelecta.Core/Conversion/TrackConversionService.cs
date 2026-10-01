using SharpSelecta.Core.Library;

namespace SharpSelecta.Core.Conversion;

public sealed record ConversionOutcome(Track Source, Track? Converted, bool OriginalRemoved, string? Error);

public sealed class TrackConversionService(IAudioConverter converter, ITrashService trash)
{
    // The converted file's length may drift by a frame or two of encoder padding, never by seconds.
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(1);

    public bool IsAvailable => converter.IsAvailable;

    public Task<SourceInfo?> ProbeAsync(string path, CancellationToken cancellationToken) => converter.ProbeAsync(path, cancellationToken);

    // Converts next to the original, carries the tags over (WAV gets none), checks the result and only then,
    // if asked, sends the original to the trash. Any failure leaves the original untouched and no partial file behind.
    public async Task<ConversionOutcome> ConvertAsync(
        Track track,
        ConversionTarget target,
        int? bitrateKbps,
        bool keepOriginal,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        string? tempPath = null;
        try
        {
            var source = await converter.ProbeAsync(track.FilePath, cancellationToken)
                         ?? throw new InvalidOperationException("The file could not be read.");
            if (ConversionRules.Check(source, target) is { } block)
                throw new InvalidOperationException($"Not convertible: {block}.");

            if (ConversionRules.IsLossy(target) && (bitrateKbps is null || !ConversionRules.AllowedBitrates(source, target).Contains(bitrateKbps.Value)))
                throw new InvalidOperationException("The chosen bitrate is not available for this file.");

            // Read fresh from the file: the Track in hand is the index's copy and may be stale.
            var current = MusicLibraryScanner.ReadTrack(track.FilePath);
            var credits = TrackCredits.Read(track.FilePath);
            var directory = Path.GetDirectoryName(track.FilePath) ?? "";
            var extension = ConversionRules.Extension(target);

            tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(track.FilePath)}.{Guid.NewGuid():N}.tmp{extension}");
            await converter.ConvertAsync(new ConversionRequest(track.FilePath, tempPath, target, bitrateKbps), progress, cancellationToken);

            if (ConversionRules.KeepsAllTags(target))
            {
                var embeddedCover = MusicLibraryScanner.LoadArtworkWithSource(track.FilePath) is { IsSeparateFile: false } artwork
                    ? new CoverArtEdit.Replace(artwork.Bytes, AsSeparateFile: false)
                    : null;
                TrackTagEditor.Write(tempPath, ToEdits(current), embeddedCover, credits);
            }
            else
            {
                TrackTagEditor.Write(tempPath, new TrackTagEdits(current.Title, current.Artist, null, current.Album, null, null, null, null));
            }

            await VerifyAsync(tempPath, source, current, cancellationToken);

            var targetPath = UniquePath(directory, Path.GetFileNameWithoutExtension(track.FilePath), extension);
            File.Move(tempPath, targetPath);
            tempPath = null;

            var converted = MusicLibraryScanner.ReadTrack(targetPath) with { DateAddedUtc = DateTime.UtcNow };

            var removed = false;
            string? error = null;
            if (!keepOriginal)
            {
                try
                {
                    trash.MoveToTrash(track.FilePath);
                    removed = true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }
            }

            return new ConversionOutcome(track, converted, removed, error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ConversionOutcome(track, null, false, exception.Message);
        }
        finally
        {
            if (tempPath is not null)
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task VerifyAsync(string path, SourceInfo source, Track expectedTags, CancellationToken cancellationToken)
    {
        var result = await converter.ProbeAsync(path, cancellationToken)
                     ?? throw new InvalidOperationException("The converted file could not be read back.");
        if ((result.Duration - source.Duration).Duration() > DurationTolerance)
            throw new InvalidOperationException("The converted file has a different length than the original.");

        var written = MusicLibraryScanner.ReadTrack(path);
        if (written.Title != expectedTags.Title || written.Artist != expectedTags.Artist || written.Album != expectedTags.Album)
            throw new InvalidOperationException("The tags did not carry over to the converted file.");
    }

    private static TrackTagEdits ToEdits(Track track) => new(
        track.Title, track.Artist, track.AlbumArtist, track.Album, track.Genre, track.Comment, track.Year, track.TrackNumber, track.DiscNumber);

    // "Song.mp3", then "Song (1).mp3", "Song (2).mp3"...
    public static string UniquePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (var n = 1; File.Exists(path); n++)
        {
            path = Path.Combine(directory, $"{name} ({n}){extension}");
        }

        return path;
    }
}
