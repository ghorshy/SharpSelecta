using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;
using TUnit.Assertions.Enums;
using static SharpSelecta.Core.Library.PlaybackBarComponent;

namespace SharpSelecta.Tests;

public class PlaybackBarEditorViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private (InterfaceSettingsViewModel Settings, PlaybackBarEditorViewModel Editor, string Path) Create(PlaybackBarLayout? saved = null)
    {
        var path = _temp.NewSettingsPath();
        if (saved is not null)
            new SettingsStore(path).Update(s => s with { PlaybackBar = saved });

        var settings = new InterfaceSettingsViewModel(new SettingsStore(path), Substitute.For<IFilePickerService>());
        return (settings, settings.PlaybackBarEditor, path);
    }

    private static IEnumerable<PlaybackBarComponent> Bar(PlaybackBarEditorViewModel editor) => editor.BarTiles.Select(t => t.Component);

    private static IEnumerable<PlaybackBarComponent> Pool(PlaybackBarEditorViewModel editor) => editor.HiddenTiles.Select(t => t.Component);

    [Test]
    public async Task Opens_WithTheSavedLayout()
    {
        var (_, editor, _) = Create(new PlaybackBarLayout { Components = [SeekBar, PlayPause, Settings], ShowTitle = false });

        await Assert.That(Bar(editor)).IsEquivalentTo([SeekBar, PlayPause, Settings], CollectionOrdering.Matching);
        await Assert.That(Pool(editor)).IsEquivalentTo([Previous, Next, Elapsed, Length, Volume, Repeat, Equalizer], CollectionOrdering.Matching);
        await Assert.That(editor.ShowTitle).IsFalse();
    }

    [Test]
    public async Task Place_MovesATileWithinTheBar()
    {
        var (settings, editor, _) = Create();

        editor.Place(PlayPause, editor.BarTiles.Count);

        await Assert.That(Bar(editor)).IsEquivalentTo(
            [Previous, Next, Elapsed, SeekBar, Length, Volume, Repeat, Equalizer, Settings, PlayPause], CollectionOrdering.Matching);
        await Assert.That(settings.PlaybackBar.Components).IsEquivalentTo(Bar(editor), CollectionOrdering.Matching);
        await Assert.That(settings.HasPendingChanges).IsTrue();
    }

    [Test]
    public async Task Place_BeforeItsOwnNeighbour_MovesItLeft()
    {
        var (_, editor, _) = Create();

        editor.Place(SeekBar, 0);

        await Assert.That(Bar(editor).Take(2)).IsEquivalentTo([SeekBar, Previous], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Place_OntoItsOwnSpot_ChangesNothing()
    {
        var (settings, editor, _) = Create();

        var moved = editor.Place(Next, 2) | editor.Place(Next, 3);

        await Assert.That(moved).IsFalse();
        await Assert.That(settings.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Hide_PutsTheTileInThePoolInDefaultOrder()
    {
        var (settings, editor, _) = Create();

        editor.Hide(Repeat);
        editor.Hide(Previous);

        await Assert.That(Pool(editor)).IsEquivalentTo([Previous, Repeat], CollectionOrdering.Matching);
        await Assert.That(settings.PlaybackBar.HiddenComponents).IsEquivalentTo([Previous, Repeat], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(SeekBar)]
    [Arguments(Settings)]
    public async Task Hide_RefusesTheLockedComponents(PlaybackBarComponent component)
    {
        var (settings, editor, _) = Create();

        var hidden = editor.Hide(component);

        await Assert.That(hidden).IsFalse();
        await Assert.That(Bar(editor)).Contains(component);
        await Assert.That(settings.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Show_PutsAHiddenTileBackAtTheEnd()
    {
        var (_, editor, _) = Create(new PlaybackBarLayout { Components = [SeekBar, Settings] });

        editor.Show(Volume);

        await Assert.That(Bar(editor)).IsEquivalentTo([SeekBar, Settings, Volume], CollectionOrdering.Matching);
        await Assert.That(Pool(editor)).DoesNotContain(Volume);
    }

    [Test]
    public async Task Show_OfATileAlreadyOnTheBar_DoesNothing()
    {
        var (settings, editor, _) = Create();

        await Assert.That(editor.Show(Volume)).IsFalse();
        await Assert.That(settings.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Place_FromThePool_InsertsAtTheDropPosition()
    {
        var (_, editor, _) = Create(new PlaybackBarLayout { Components = [PlayPause, SeekBar, Settings] });

        editor.Place(Volume, 1);

        await Assert.That(Bar(editor)).IsEquivalentTo([PlayPause, Volume, SeekBar, Settings], CollectionOrdering.Matching);
    }

    [Test]
    public async Task MoveBy_StopsAtTheEdges()
    {
        var (settings, editor, _) = Create();

        var left = editor.MoveBy(Previous, -1);
        var right = editor.MoveBy(Settings, 1);

        await Assert.That(left).IsFalse();
        await Assert.That(right).IsFalse();
        await Assert.That(settings.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task MoveBy_SwapsWithTheNeighbour()
    {
        var (_, editor, _) = Create();

        editor.MoveBy(Next, -1);

        await Assert.That(Bar(editor).Take(3)).IsEquivalentTo([Previous, Next, PlayPause], CollectionOrdering.Matching);
    }

    [Test]
    public async Task SeekBarOptions_AreEdits()
    {
        var (settings, editor, _) = Create();

        editor.SelectedSeekBarWidthOption = editor.SeekBarWidthOptions.Single(o => o.Width == SeekBarWidth.Fixed);
        editor.SeekBarPercent = 35;
        editor.SelectedAlignmentOption = editor.AlignmentOptions.Single(o => o.Alignment == PlaybackBarAlignment.Right);
        editor.ShowTitle = false;
        editor.SelectedTitleAlignmentOption = editor.TitleAlignmentOptions.Single(o => o.Alignment == TitleAlignment.Center);

        await Assert.That(editor.IsSeekBarFixed).IsTrue();
        await Assert.That(settings.PlaybackBar.SeekBarWidth).IsEqualTo(SeekBarWidth.Fixed);
        await Assert.That(settings.PlaybackBar.SeekBarPercent).IsEqualTo(35);
        await Assert.That(settings.PlaybackBar.Alignment).IsEqualTo(PlaybackBarAlignment.Right);
        await Assert.That(settings.PlaybackBar.ShowTitle).IsFalse();
        await Assert.That(settings.PlaybackBar.TitleAlignment).IsEqualTo(TitleAlignment.Center);
    }

    [Test]
    public async Task RestoreDefault_BringsBackTodaysBar_AndIsOnlyAvailableWhenItDiffers()
    {
        var (settings, editor, _) = Create(new PlaybackBarLayout { Components = [SeekBar, Settings], ShowTitle = false });

        await Assert.That(editor.RestoreDefaultCommand.CanExecute(null)).IsTrue();
        editor.RestoreDefaultCommand.Execute(null);

        await Assert.That(settings.PlaybackBar).IsEqualTo(PlaybackBarLayout.Default);
        await Assert.That(Pool(editor)).IsEmpty();
        await Assert.That(editor.RestoreDefaultCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Apply_SavesTheEditedBar()
    {
        var (settings, editor, path) = Create();

        editor.Hide(Equalizer);
        settings.ApplyCommand.Execute(null);

        await Assert.That(new SettingsStore(path).Current.PlaybackBar.HiddenComponents).IsEquivalentTo([Equalizer]);
        await Assert.That(settings.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Cancel_PutsTheAppliedBarBack_AndTellsThePreview()
    {
        var (settings, editor, path) = Create();
        var raised = new List<string?>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        editor.Hide(Equalizer);
        editor.ShowTitle = false;
        raised.Clear();
        settings.CancelCommand.Execute(null);

        await Assert.That(settings.PlaybackBar).IsEqualTo(PlaybackBarLayout.Default);
        await Assert.That(Bar(editor)).Contains(Equalizer);
        await Assert.That(editor.ShowTitle).IsTrue();
        await Assert.That(raised).Contains(nameof(InterfaceSettingsViewModel.PlaybackBar));
        await Assert.That(settings.HasPendingChanges).IsFalse();
        await Assert.That(new SettingsStore(path).Current.PlaybackBar).IsEqualTo(PlaybackBarLayout.Default);
    }

    [Test]
    public async Task UseWaveformSlider_IsAnInterfaceEdit_SavedOnApplyAndRevertedOnCancel()
    {
        var (settings, _, path) = Create();

        settings.UseWaveformSlider = true;
        await Assert.That(settings.HasPendingChanges).IsTrue();
        settings.CancelCommand.Execute(null);
        await Assert.That(settings.UseWaveformSlider).IsFalse();

        settings.UseWaveformSlider = true;
        settings.ApplyCommand.Execute(null);
        await Assert.That(new SettingsStore(path).Current.UseWaveformSlider).IsTrue();
    }
}
