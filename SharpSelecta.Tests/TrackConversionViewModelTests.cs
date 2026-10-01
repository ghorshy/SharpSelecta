using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Conversion;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrackConversionViewModelTests
{
    private static readonly SourceInfo Flac = new("flac", true, null, 44100, 16, 2, TimeSpan.FromSeconds(10));
    private static readonly SourceInfo Mp3At160 = new("mp3", false, 160, 44100, null, 2, TimeSpan.FromSeconds(10));

    private static async Task<TrackConversionViewModel> CreateAsync(bool available, params SourceInfo[] sources)
    {
        var converter = Substitute.For<IAudioConverter>();
        converter.IsAvailable.Returns(available);
        var tracks = sources.Select((_, i) => new Track($"/music/{i}.x", $"{i}")).ToList();
        for (var i = 0; i < sources.Length; i++)
        {
            converter.ProbeAsync(tracks[i].FilePath, Arg.Any<CancellationToken>()).Returns(sources[i]);
        }

        var viewModel = new TrackConversionViewModel(tracks, new TrackConversionService(converter, Substitute.For<ITrashService>()), NullLogger.Instance);
        await viewModel.LoadTask;
        return viewModel;
    }

    private static ConversionTargetOption Option(TrackConversionViewModel viewModel, ConversionTarget target) =>
        viewModel.Targets.Single(t => t.Target == target);

    [Test]
    public async Task KeepOriginal_DefaultsToTrue()
    {
        var viewModel = await CreateAsync(true, Flac);

        await Assert.That(viewModel.KeepOriginal).IsTrue();
        await Assert.That(viewModel.ShowTrashWarning).IsFalse();
    }

    [Test]
    public async Task UnticksKeepOriginal_ShowsTheTrashWarning()
    {
        var viewModel = await CreateAsync(true, Flac);

        viewModel.KeepOriginal = false;

        await Assert.That(viewModel.ShowTrashWarning).IsTrue();
    }

    [Test]
    public async Task LossySourceToLosslessTarget_CannotBeConverted()
    {
        var viewModel = await CreateAsync(true, Mp3At160);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.Flac);

        await Assert.That(viewModel.EligibleCount).IsEqualTo(0);
        await Assert.That(viewModel.ConvertCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsEqualTo(Strings.ConvertBlockLossyToLossless);
    }

    [Test]
    public async Task LossySource_OffersNoBitrateAboveItsOwn()
    {
        var viewModel = await CreateAsync(true, Mp3At160);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.AacM4a);

        await Assert.That(viewModel.BitrateOptions.Select(o => o.Kbps)).IsEquivalentTo([160, 128, 96, 64]);
        await Assert.That(viewModel.SelectedBitrate!.Kbps).IsEqualTo(160);
        await Assert.That(viewModel.ConvertCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task LosslessSource_OffersTheFullBitrateList()
    {
        var viewModel = await CreateAsync(true, Flac);

        await Assert.That(viewModel.BitrateOptions.Select(o => o.Kbps)).IsEquivalentTo([320, 256, 224, 192, 160, 128, 96, 64]);
    }

    [Test]
    public async Task LosslessTarget_HasNoBitrateChoice()
    {
        var viewModel = await CreateAsync(true, Flac);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.AlacM4a);

        await Assert.That(viewModel.HasBitrateChoice).IsFalse();
        await Assert.That(viewModel.ConvertCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task MixedSelection_SkipsTheTracksThatCannotGoToTheTarget()
    {
        var viewModel = await CreateAsync(true, Flac, Mp3At160);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.Flac);

        await Assert.That(viewModel.EligibleCount).IsEqualTo(0);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.AlacM4a);

        await Assert.That(viewModel.EligibleCount).IsEqualTo(1);
        await Assert.That(viewModel.StatusMessage).IsEqualTo(Strings.ConvertSkipped(1, 2));
    }

    [Test]
    public async Task WithoutAConverter_ExplainsAndCannotConvert()
    {
        var viewModel = await CreateAsync(false, Flac);

        await Assert.That(viewModel.IsAvailable).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsEqualTo(Strings.ConverterUnavailable);
        await Assert.That(viewModel.ConvertCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task FromSummary_ForOneTrack_NamesTheFormatAndWhetherItIsLossless()
    {
        var viewModel = await CreateAsync(true, Flac);

        await Assert.That(viewModel.FromSummary).IsEqualTo(Strings.ConvertSourceKind("FLAC", Strings.ConvertKindLossless));
    }

    [Test]
    public async Task FromSummary_ForSeveralTracks_CountsThemAndGroupsTheFormats()
    {
        var viewModel = await CreateAsync(true, Flac, Flac, Mp3At160);

        await Assert.That(viewModel.FromSummary).IsEqualTo(Strings.ConvertTrackCount(3));
        await Assert.That(viewModel.FromDetails).Contains(Strings.ConvertGroup(2, Strings.ConvertSourceKind("FLAC", Strings.ConvertKindLossless)));
    }

    [Test]
    public async Task ShowWavTagsHint_OnlyForWav()
    {
        var viewModel = await CreateAsync(true, Flac);

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.Wav);
        await Assert.That(viewModel.ShowWavTagsHint).IsTrue();

        viewModel.SelectedTarget = Option(viewModel, ConversionTarget.Flac);
        await Assert.That(viewModel.ShowWavTagsHint).IsFalse();
    }
}
