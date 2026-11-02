using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class SettingsStoreTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-settings-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task WhenFileDoesNotExist_CurrentHoldsTheDefaults()
    {
        var settings = new SettingsStore(CreateTempSettingsPath()).Current;

        await Assert.That(settings.RestoreQueueOnStartup).IsTrue();
        await Assert.That(settings.SeekStepSeconds).IsEqualTo(5);
        await Assert.That(settings.Theme).IsEqualTo(AppTheme.Dark);
        await Assert.That(settings.VolumeCurve).IsEqualTo(VolumeCurve.Linear);
        await Assert.That(settings.CheckForUpdatesAutomatically).IsTrue();
        await Assert.That(settings.AutoDjEnabled).IsFalse();
        await Assert.That(settings.DiscordPresenceEnabled).IsFalse();
        await Assert.That(settings.EqualizerEnabled).IsFalse();
        await Assert.That(settings.UseWaveformSlider).IsFalse();
        await Assert.That(settings.ViewMode).IsEqualTo(LibraryViewMode.TrackList);
        await Assert.That(settings.LibrarySection).IsEqualTo(LibrarySection.Library);
        await Assert.That(settings.AlbumSortMode).IsEqualTo(AlbumSortMode.Title);
        await Assert.That(settings.Volume).IsNull();
        await Assert.That(settings.LibraryFolderPaths).IsNull();
    }

    [Test]
    public async Task Update_IsVisibleImmediately_AndSurvivesANewInstance()
    {
        var path = CreateTempSettingsPath();
        try
        {
            var store = new SettingsStore(path);

            store.Update(s => s with { Volume = 0.42, LibraryFolderPaths = ["/music"], Theme = AppTheme.Light, SeekStepSeconds = 15 });

            await Assert.That(store.Current.Volume).IsEqualTo(0.42);
            var reloaded = new SettingsStore(path).Current;
            await Assert.That(reloaded.Volume).IsEqualTo(0.42);
            await Assert.That(reloaded.LibraryFolderPaths).IsEquivalentTo(["/music"]);
            await Assert.That(reloaded.Theme).IsEqualTo(AppTheme.Light);
            await Assert.That(reloaded.SeekStepSeconds).IsEqualTo(15);
            await Assert.That(reloaded.RestoreQueueOnStartup).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task EverySetting_RoundTripsThroughTheFile()
    {
        var path = CreateTempSettingsPath();
        try
        {
            new SettingsStore(path).Update(_ => new AppSettings
            {
                LibraryFolderPaths = ["/music/a", "/music/b"],
                Columns = new Dictionary<string, bool> { ["Artist"] = false },
                ColumnOrder = ["Title", "Artist"],
                RightColumnWidth = 321,
                ColumnWidths = new Dictionary<string, double> { ["Title"] = 250 },
                SortPropertyPath = "Album",
                SortDescending = true,
                TileSize = 180,
                ViewMode = LibraryViewMode.AlbumGrid,
                LibrarySection = LibrarySection.Playlist,
                RecentlyAddedViewMode = LibraryViewMode.AlbumGrid,
                SelectedPlaylistId = "playlist-1",
                AlbumSortMode = AlbumSortMode.Artist,
                AlbumSortDescending = true,
                RestoreQueueOnStartup = false,
                OutputDeviceName = "Speakers",
                Volume = 0.3,
                VolumeCurve = VolumeCurve.Logarithmic,
                SeekStepSeconds = 10,
                ShortcutOverrides = new Dictionary<string, string> { ["PlayPause"] = "Ctrl+P" },
                Theme = AppTheme.Custom,
                CustomThemeFileName = "mine.axaml",
                EqualizerEnabled = true,
                EqualizerPresetName = "Rock",
                EqualizerBandGainsDb = [1.5f, -2f],
                UseWaveformSlider = true,
                AutoDjEnabled = true,
                AutoDjSourcePlaylistId = "playlist-2",
                DiscordPresenceEnabled = true,
                CheckForUpdatesAutomatically = false,
                FanartApiKey = "key",
            });

            var s = new SettingsStore(path).Current;

            await Assert.That(s.LibraryFolderPaths).IsEquivalentTo(["/music/a", "/music/b"]);
            await Assert.That(s.Columns!["Artist"]).IsFalse();
            await Assert.That(s.ColumnOrder).IsEquivalentTo(["Title", "Artist"]);
            await Assert.That(s.RightColumnWidth).IsEqualTo(321);
            await Assert.That(s.ColumnWidths!["Title"]).IsEqualTo(250);
            await Assert.That(s.SortPropertyPath).IsEqualTo("Album");
            await Assert.That(s.SortDescending).IsTrue();
            await Assert.That(s.TileSize).IsEqualTo(180);
            await Assert.That(s.ViewMode).IsEqualTo(LibraryViewMode.AlbumGrid);
            await Assert.That(s.LibrarySection).IsEqualTo(LibrarySection.Playlist);
            await Assert.That(s.RecentlyAddedViewMode).IsEqualTo(LibraryViewMode.AlbumGrid);
            await Assert.That(s.SelectedPlaylistId).IsEqualTo("playlist-1");
            await Assert.That(s.AlbumSortMode).IsEqualTo(AlbumSortMode.Artist);
            await Assert.That(s.AlbumSortDescending).IsTrue();
            await Assert.That(s.RestoreQueueOnStartup).IsFalse();
            await Assert.That(s.OutputDeviceName).IsEqualTo("Speakers");
            await Assert.That(s.Volume).IsEqualTo(0.3);
            await Assert.That(s.VolumeCurve).IsEqualTo(VolumeCurve.Logarithmic);
            await Assert.That(s.SeekStepSeconds).IsEqualTo(10);
            await Assert.That(s.ShortcutOverrides!["PlayPause"]).IsEqualTo("Ctrl+P");
            await Assert.That(s.Theme).IsEqualTo(AppTheme.Custom);
            await Assert.That(s.CustomThemeFileName).IsEqualTo("mine.axaml");
            await Assert.That(s.EqualizerEnabled).IsTrue();
            await Assert.That(s.EqualizerPresetName).IsEqualTo("Rock");
            await Assert.That(s.EqualizerBandGainsDb).IsEquivalentTo([1.5f, -2f]);
            await Assert.That(s.UseWaveformSlider).IsTrue();
            await Assert.That(s.AutoDjEnabled).IsTrue();
            await Assert.That(s.AutoDjSourcePlaylistId).IsEqualTo("playlist-2");
            await Assert.That(s.DiscordPresenceEnabled).IsTrue();
            await Assert.That(s.CheckForUpdatesAutomatically).IsFalse();
            await Assert.That(s.FanartApiKey).IsEqualTo("key");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AFileWithExplicitNulls_LoadsWithTheDefaultsForThoseSettings()
    {
        var path = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"LibraryFolderPaths":["/music"],"RestoreQueueOnStartup":null,"SeekStepSeconds":null,"Theme":null,"Volume":0.7}""");

            var settings = new SettingsStore(path).Current;

            await Assert.That(settings.LibraryFolderPaths).IsEquivalentTo(["/music"]);
            await Assert.That(settings.Volume).IsEqualTo(0.7);
            await Assert.That(settings.RestoreQueueOnStartup).IsTrue();
            await Assert.That(settings.SeekStepSeconds).IsEqualTo(5);
            await Assert.That(settings.Theme).IsEqualTo(AppTheme.Dark);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ACorruptFile_LoadsAsDefaults_AndIsKeptAsideOnTheFirstUpdate()
    {
        var path = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(path, "{ not valid json");

            var store = new SettingsStore(path);
            await Assert.That(store.Current.Volume).IsNull();

            store.Update(s => s with { Volume = 0.5 });

            await Assert.That(new SettingsStore(path).Current.Volume).IsEqualTo(0.5);
            await Assert.That(File.ReadAllText(path + ".corrupt")).IsEqualTo("{ not valid json");
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".corrupt");
        }
    }

    [Test]
    public async Task AnUnreadableFile_LoadsAsDefaults_AndIsNeverOverwritten()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes only");
        }

        var path = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"Volume":0.7}""");
            File.SetUnixFileMode(path, UnixFileMode.None);

            var store = new SettingsStore(path);
            store.Update(s => s with { Volume = 0.1 });

            await Assert.That(store.Current.Volume).IsEqualTo(0.1);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await Assert.That(File.ReadAllText(path)).IsEqualTo("""{"Volume":0.7}""");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AFileThatBecomesReadable_IsReadAgainBeforeTheNextUpdateWritesIt()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes only");
        }

        var path = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(path, """{"Volume":0.7,"SeekStepSeconds":12}""");
            File.SetUnixFileMode(path, UnixFileMode.None);
            var store = new SettingsStore(path);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            store.Update(s => s with { Volume = 0.1 });

            var reloaded = new SettingsStore(path).Current;
            await Assert.That(reloaded.Volume).IsEqualTo(0.1);
            await Assert.That(reloaded.SeekStepSeconds).IsEqualTo(12);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ConcurrentUpdatesOfDifferentSettings_AllSurvive()
    {
        var path = CreateTempSettingsPath();
        try
        {
            var store = new SettingsStore(path);
            var updates = new Func<AppSettings, AppSettings>[]
            {
                s => s with { Volume = 0.42 },
                s => s with { TileSize = 180 },
                s => s with { SeekStepSeconds = 15 },
                s => s with { AutoDjEnabled = true },
                s => s with { DiscordPresenceEnabled = true },
                s => s with { UseWaveformSlider = true },
                s => s with { EqualizerEnabled = true },
                s => s with { Theme = AppTheme.Light },
            };

            await Task.WhenAll(updates.Select(u => Task.Run(() => store.Update(u))));

            var s = new SettingsStore(path).Current;
            await Assert.That(s.Volume).IsEqualTo(0.42);
            await Assert.That(s.TileSize).IsEqualTo(180);
            await Assert.That(s.SeekStepSeconds).IsEqualTo(15);
            await Assert.That(s.AutoDjEnabled).IsTrue();
            await Assert.That(s.DiscordPresenceEnabled).IsTrue();
            await Assert.That(s.UseWaveformSlider).IsTrue();
            await Assert.That(s.EqualizerEnabled).IsTrue();
            await Assert.That(s.Theme).IsEqualTo(AppTheme.Light);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task FanartApiKey_IsStoredTrimmed_AndABlankValueClearsIt()
    {
        await Assert.That(new AppSettings { FanartApiKey = "  abc  " }.FanartApiKey).IsEqualTo("abc");
        await Assert.That(new AppSettings { FanartApiKey = "   " }.FanartApiKey).IsNull();
    }
}
