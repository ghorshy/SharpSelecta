using SharpSelecta.Core.Conversion;
using SharpSelecta.Core.Library;
using SharpSelecta.Integrations.Conversion;

namespace SharpSelecta.Tests;

public class TrackConversionServiceTests
{
    private sealed class FakeTrash : ITrashService
    {
        public List<string> Trashed { get; } = [];

        public bool Fails { get; set; }

        public void MoveToTrash(string path)
        {
            if (Fails)
                throw new IOException("no trash");

            Trashed.Add(path);
            File.Delete(path);
        }
    }

    private static readonly FfmpegAudioConverter Converter = new();

    private static string CreateTaggedCopy(string directory, string fixtureName)
    {
        var path = Path.Combine(directory, fixtureName);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName), path);
        TrackTagEditor.Write(path, new TrackTagEdits("Song", "Artist", "Album Artist", "Album", "Rock", "Note", 2020, 3, 1));
        return path;
    }

    // The fixtures are 8 kHz mono, which libvorbis can't encode at the offered bitrates.
    private static string CreateTaggedStereoFlac(string directory)
    {
        var path = Path.Combine(directory, "stereo.flac");
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ffmpeg",
            ["-v", "error", "-f", "lavfi", "-i", "sine=duration=1:sample_rate=44100", "-ac", "2", path]));
        process!.WaitForExit();
        TrackTagEditor.Write(path, new TrackTagEdits("Song", "Artist", "Album Artist", "Album", "Rock", "Note", 2020, 3, 1));
        return path;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sharpselecta-convert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static Task<ConversionOutcome> ConvertAsync(TrackConversionService service, string path, ConversionTarget target, int? bitrate = null, bool keepOriginal = true) =>
        service.ConvertAsync(MusicLibraryScanner.ReadTrackIfExists(path)!, target, bitrate, keepOriginal, new Progress<double>(), CancellationToken.None);

    [Test]
    public async Task Convert_FlacToAlac_CarriesTagsAndKeepsTheOriginal()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.AlacM4a);

            await Assert.That(outcome.Error).IsNull();
            await Assert.That(outcome.OriginalRemoved).IsFalse();
            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(outcome.Converted!.FilePath).IsEqualTo(Path.Combine(directory, "untagged-track.m4a"));
            await Assert.That(outcome.Converted.Title).IsEqualTo("Song");
            await Assert.That(outcome.Converted.AlbumArtist).IsEqualTo("Album Artist");
            await Assert.That(outcome.Converted.TrackNumber).IsEqualTo(3);
            await Assert.That(Directory.GetFiles(directory, ".*")).IsEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_ToWav_KeepsOnlyTitleArtistAndAlbum()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.Wav);

            await Assert.That(outcome.Error).IsNull();
            await Assert.That(outcome.Converted!.Title).IsEqualTo("Song");
            await Assert.That(outcome.Converted.Artist).IsEqualTo("Artist");
            await Assert.That(outcome.Converted.Album).IsEqualTo("Album");
            await Assert.That(outcome.Converted.Genre).IsNull();
            await Assert.That(outcome.Converted.TrackNumber).IsNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_WithoutKeepingTheOriginal_SendsItToTheTrash()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        var trash = new FakeTrash();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, trash), path, ConversionTarget.AlacM4a, keepOriginal: false);

            await Assert.That(outcome.OriginalRemoved).IsTrue();
            await Assert.That(trash.Trashed).IsEquivalentTo([path]);
            await Assert.That(File.Exists(outcome.Converted!.FilePath)).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_WhenTheTrashFails_KeepsBothFilesAndReportsIt()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash { Fails = true }), path, ConversionTarget.AlacM4a, keepOriginal: false);

            await Assert.That(outcome.OriginalRemoved).IsFalse();
            await Assert.That(outcome.Error).IsNotNull();
            await Assert.That(outcome.Converted).IsNotNull();
            await Assert.That(File.Exists(path)).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_WhenTheTargetNameIsTaken_AddsANumber()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");
            File.WriteAllText(Path.Combine(directory, "untagged-track.m4a"), "taken");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.AlacM4a);

            await Assert.That(outcome.Converted!.FilePath).IsEqualTo(Path.Combine(directory, "untagged-track (1).m4a"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_LossyToLossless_IsRefusedAndLeavesNothingBehind()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.mp3");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.Flac);

            await Assert.That(outcome.Converted).IsNull();
            await Assert.That(outcome.Error).IsNotNull();
            await Assert.That(Directory.GetFiles(directory)).HasCount().EqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_FlacToOggVorbis_CarriesAllTags()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedStereoFlac(directory);

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.OggVorbis, 128);

            await Assert.That(outcome.Error).IsNull();
            await Assert.That(outcome.Converted!.FilePath).IsEqualTo(Path.Combine(directory, "stereo.ogg"));
            await Assert.That(outcome.Converted.Title).IsEqualTo("Song");
            await Assert.That(outcome.Converted.AlbumArtist).IsEqualTo("Album Artist");
            await Assert.That(outcome.Converted.Genre).IsEqualTo("Rock");
            await Assert.That(outcome.Converted.TrackNumber).IsEqualTo(3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_FlacToAiff_CarriesAllTags()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.flac");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.Aiff);

            await Assert.That(outcome.Error).IsNull();
            await Assert.That(outcome.Converted!.FilePath).IsEqualTo(Path.Combine(directory, "untagged-track.aiff"));
            await Assert.That(outcome.Converted.Title).IsEqualTo("Song");
            await Assert.That(outcome.Converted.Artist).IsEqualTo("Artist");
            await Assert.That(outcome.Converted.Album).IsEqualTo("Album");
            await Assert.That(outcome.Converted.Genre).IsEqualTo("Rock");
            await Assert.That(outcome.Converted.TrackNumber).IsEqualTo(3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_AiffToWav_IsAllowedAndWavToAiffToo()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var aiff = CreateTaggedCopy(directory, "untagged-track.aiff");
            var service = new TrackConversionService(Converter, new FakeTrash());

            var toWav = await ConvertAsync(service, aiff, ConversionTarget.Wav);
            var backToAiff = await ConvertAsync(service, toWav.Converted!.FilePath, ConversionTarget.Aiff);

            await Assert.That(toWav.Error).IsNull();
            await Assert.That(backToAiff.Error).IsNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Convert_OggVorbisToFlac_IsRefusedAsLossyToLossless()
    {
        Skip.When(!Converter.IsAvailable, "ffmpeg is not installed");
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateTaggedCopy(directory, "untagged-track.ogg");

            var outcome = await ConvertAsync(new TrackConversionService(Converter, new FakeTrash()), path, ConversionTarget.Flac);

            await Assert.That(outcome.Converted).IsNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UniquePath_SkipsTakenNames()
    {
        var directory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "a.mp3"), "");
            File.WriteAllText(Path.Combine(directory, "a (1).mp3"), "");

            await Assert.That(TrackConversionService.UniquePath(directory, "a", ".mp3")).IsEqualTo(Path.Combine(directory, "a (2).mp3"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
