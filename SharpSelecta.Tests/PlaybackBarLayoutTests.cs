using SharpSelecta.Core.Library;
using TUnit.Assertions.Enums;
using static SharpSelecta.Core.Library.PlaybackBarComponent;

namespace SharpSelecta.Tests;

public class PlaybackBarLayoutTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Test]
    public async Task Default_IsTodaysBar()
    {
        var layout = PlaybackBarLayout.Default;

        await Assert.That(layout.Components).IsEquivalentTo(
            [Previous, PlayPause, Next, Elapsed, SeekBar, Length, Volume, Repeat, Equalizer, Settings], CollectionOrdering.Matching);
        await Assert.That(layout.SeekBarWidth).IsEqualTo(SeekBarWidth.Fill);
        await Assert.That(layout.ShowTitle).IsTrue();
        await Assert.That(layout.HiddenComponents).IsEmpty();
    }

    [Test]
    public async Task Normalize_DropsDuplicates()
    {
        var layout = new PlaybackBarLayout { Components = [PlayPause, SeekBar, PlayPause, Settings, Settings] }.Normalize();

        await Assert.That(layout.Components).IsEquivalentTo([PlayPause, SeekBar, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Normalize_PutsTheSeekBarBackAfterTheComponentsThatPrecedeItByDefault()
    {
        var layout = new PlaybackBarLayout { Components = [Next, Repeat, Previous, Settings] }.Normalize();

        await Assert.That(layout.Components).IsEquivalentTo([Next, Repeat, Previous, SeekBar, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Normalize_PutsTheSeekBarFirstWhenNothingThatPrecedesItIsShown()
    {
        var layout = new PlaybackBarLayout { Components = [Volume, Settings] }.Normalize();

        await Assert.That(layout.Components).IsEquivalentTo([SeekBar, Volume, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Normalize_PutsSettingsBackAtTheEnd()
    {
        var layout = new PlaybackBarLayout { Components = [Volume, SeekBar, PlayPause] }.Normalize();

        await Assert.That(layout.Components).IsEquivalentTo([Volume, SeekBar, PlayPause, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Normalize_DropsValuesOutsideTheEnum()
    {
        var layout = new PlaybackBarLayout { Components = [(PlaybackBarComponent)99, SeekBar, Settings] }.Normalize();

        await Assert.That(layout.Components).IsEquivalentTo([SeekBar, Settings], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(5, 20)]
    [Arguments(95, 80)]
    [Arguments(47, 45)]
    [Arguments(48, 50)]
    [Arguments(60, 60)]
    public async Task Normalize_ClampsThePercentToTheSlidersRangeAndStep(int saved, int expected)
    {
        var layout = new PlaybackBarLayout { SeekBarPercent = saved }.Normalize();

        await Assert.That(layout.SeekBarPercent).IsEqualTo(expected);
    }

    [Test]
    public async Task HiddenComponents_AreTheOnesNotOnTheBar()
    {
        var layout = new PlaybackBarLayout { Components = [PlayPause, SeekBar, Settings] };

        await Assert.That(layout.HiddenComponents).IsEquivalentTo([Previous, Next, Elapsed, Length, Volume, Repeat, Equalizer], CollectionOrdering.Matching);
    }

    [Test]
    public async Task OnlyTheSeekBarAndSettingsCantBeHidden()
    {
        var locked = PlaybackBarLayout.AllComponents.Where(c => !PlaybackBarLayout.CanHide(c));

        await Assert.That(locked).IsEquivalentTo([SeekBar, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Equality_ComparesTheComponentsByValue()
    {
        var a = new PlaybackBarLayout { Components = [PlayPause, SeekBar, Settings] };
        var b = new PlaybackBarLayout { Components = new List<PlaybackBarComponent> { PlayPause, SeekBar, Settings } };

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
        await Assert.That(a with { ShowTitle = false }).IsNotEqualTo(b);
    }

    [Test]
    public async Task Settings_WithoutALayout_LoadTheDefault()
    {
        var path = _temp.NewSettingsPath();
        await File.WriteAllTextAsync(path, """{ "Theme": 1 }""");

        var settings = new SettingsStore(path).Current;

        await Assert.That(settings.PlaybackBar).IsEqualTo(PlaybackBarLayout.Default);
    }

    [Test]
    public async Task Settings_RoundTripTheLayout()
    {
        var path = _temp.NewSettingsPath();
        var layout = new PlaybackBarLayout
        {
            Components = [SeekBar, PlayPause, Volume, Settings],
            SeekBarWidth = SeekBarWidth.Fixed,
            SeekBarPercent = 35,
            Alignment = PlaybackBarAlignment.Right,
            ShowTitle = false,
        };
        new SettingsStore(path).Update(s => s with { PlaybackBar = layout });

        var reloaded = new SettingsStore(path).Current.PlaybackBar;

        await Assert.That(reloaded).IsEqualTo(layout);
    }

    [Test]
    public async Task Settings_SaveComponentsByName()
    {
        var path = _temp.NewSettingsPath();
        new SettingsStore(path).Update(s => s with { PlaybackBar = new PlaybackBarLayout { Components = [SeekBar, Settings] } });

        var json = await File.ReadAllTextAsync(path);

        await Assert.That(json).Contains("""["SeekBar","Settings"]""");
    }

    [Test]
    public async Task Settings_WithAnUnknownOrMissingComponent_DropItAndRestoreTheLockedOnes()
    {
        var path = _temp.NewSettingsPath();
        await File.WriteAllTextAsync(path, """{ "PlaybackBar": { "Components": ["PlayPause", "Lyrics", 3, "Next", "Next"], "SeekBarPercent": 99 } }""");

        var layout = new SettingsStore(path).Current.PlaybackBar;

        await Assert.That(layout.Components).IsEquivalentTo([PlayPause, Next, SeekBar, Settings], CollectionOrdering.Matching);
        await Assert.That(layout.SeekBarPercent).IsEqualTo(80);
    }
}
