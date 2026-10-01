using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Conversion;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed record ConversionTargetOption(ConversionTarget Target, string Label);

public sealed record BitrateOption(int Kbps)
{
    public string Label => Strings.BitrateKbps(Kbps);
}

// The Convert tab: converts the tracks being edited to another format next to the originals. A track that
// can't go to the chosen format (lossy to lossless, same format, bitrate above its own) is skipped, not an error.
public sealed partial class TrackConversionViewModel : ViewModelBase
{
    private readonly TrackConversionService _service;
    private readonly ILogger _logger;
    private readonly List<Track> _tracks;
    private readonly List<SourceInfo?> _sources;
    private CancellationTokenSource? _cancellation;

    public TrackConversionViewModel(IReadOnlyList<Track> tracks, TrackConversionService service, ILogger logger)
    {
        _tracks = [.. tracks];
        _sources = [.. _tracks.Select(_ => (SourceInfo?)null)];
        _service = service;
        _logger = logger;

        Targets =
        [
            new(ConversionTarget.Mp3, Strings.ConvertFormatMp3),
            new(ConversionTarget.Flac, Strings.ConvertFormatFlac),
            new(ConversionTarget.AlacM4a, Strings.ConvertFormatAlac),
            new(ConversionTarget.AacM4a, Strings.ConvertFormatAac),
            new(ConversionTarget.OggVorbis, Strings.ConvertFormatOgg),
            new(ConversionTarget.Wav, Strings.ConvertFormatWav),
            new(ConversionTarget.Aiff, Strings.ConvertFormatAiff),
        ];
        SelectedTarget = Targets[0];

        IsProbing = service.IsAvailable;
        LoadTask = LoadAsync();
    }

    // Completes once the files have been probed.
    public Task LoadTask { get; }

    public IReadOnlyList<ConversionTargetOption> Targets { get; }

    public ObservableCollection<BitrateOption> BitrateOptions { get; } = [];

    public bool IsAvailable => _service.IsAvailable;

    // Raised once per run with every track that was converted (also when the run was cancelled part-way).
    public event EventHandler<IReadOnlyList<ConversionOutcome>>? Converted;

    [ObservableProperty]
    public partial ConversionTargetOption SelectedTarget { get; set; }

    [ObservableProperty]
    public partial BitrateOption? SelectedBitrate { get; set; }

    [ObservableProperty]
    public partial bool KeepOriginal { get; set; } = true;

    [ObservableProperty]
    public partial bool IsProbing { get; set; }

    [ObservableProperty]
    public partial bool IsConverting { get; set; }

    // 0..100 across the whole batch.
    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ResultMessage { get; set; }

    [ObservableProperty]
    public partial int EligibleCount { get; set; }

    // The "from" side: what the selected tracks are now.
    [ObservableProperty]
    public partial string FromSummary { get; set; } = "";

    [ObservableProperty]
    public partial string? FromDetails { get; set; }

    public bool ShowWavTagsHint => SelectedTarget.Target == ConversionTarget.Wav;

    public bool HasBitrateChoice => BitrateOptions.Count > 0;

    public bool ShowTrashWarning => !KeepOriginal;

    private bool CanConvert => IsAvailable && !IsProbing && !IsConverting && EligibleCount > 0;

    partial void OnSelectedTargetChanged(ConversionTargetOption value)
    {
        OnPropertyChanged(nameof(ShowWavTagsHint));
        RebuildBitrates();
        Refresh();
    }

    partial void OnSelectedBitrateChanged(BitrateOption? value) => Refresh();

    partial void OnKeepOriginalChanged(bool value) => OnPropertyChanged(nameof(ShowTrashWarning));

    partial void OnIsProbingChanged(bool value)
    {
        Refresh();
        ConvertCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsConvertingChanged(bool value) => ConvertCommand.NotifyCanExecuteChanged();

    partial void OnEligibleCountChanged(int value) => ConvertCommand.NotifyCanExecuteChanged();

    private async Task LoadAsync()
    {
        if (!IsAvailable)
        {
            Refresh();
            return;
        }

        var probed = await Task.WhenAll(_tracks.Select(track => _service.ProbeAsync(track.FilePath, CancellationToken.None)));
        _sources.Clear();
        _sources.AddRange(probed);

        RebuildBitrates();
        UpdateFrom();
        IsProbing = false;
    }

    private static string FormatName(SourceInfo source) => source.Codec switch
    {
        "vorbis" => Strings.ConvertFormatOgg,
        _ when ConversionRules.IsPcm(source.Codec) => ConversionRules.IsBigEndian(source.Codec) ? Strings.ConvertFormatAiff : Strings.ConvertFormatWav,
        _ => source.Codec.ToUpperInvariant(),
    };

    private static string Describe(SourceInfo source) =>
        Strings.ConvertSourceKind(FormatName(source), source.IsLossless ? Strings.ConvertKindLossless : Strings.ConvertKindLossy);

    private void UpdateFrom()
    {
        var known = _sources.OfType<SourceInfo>().ToList();
        if (known.Count == 0)
        {
            FromSummary = _tracks.Count > 1 ? Strings.ConvertTrackCount(_tracks.Count) : "";
            FromDetails = null;
            return;
        }

        if (_tracks.Count == 1)
        {
            var source = known[0];
            FromSummary = Describe(source);
            FromDetails = string.Join(" · ", new[]
            {
                source.BitrateKbps is { } bitrate ? TrackFormatting.FormatBitrate(bitrate) : "",
                TrackFormatting.FormatSampleRate(source.SampleRate),
                source.BitDepth is { } depth ? TrackFormatting.FormatBitDepth(depth) : "",
            }.Where(part => part.Length > 0));
            return;
        }

        FromSummary = Strings.ConvertTrackCount(_tracks.Count);
        FromDetails = string.Join(", ", known
            .GroupBy(Describe)
            .OrderByDescending(group => group.Count())
            .Select(group => Strings.ConvertGroup(group.Count(), group.Key)));
    }

    // Bitrates any convertible track can go to; each track still checks its own ceiling when it is converted.
    private void RebuildBitrates()
    {
        var target = SelectedTarget.Target;
        var offered = _sources
            .Where(source => source is not null && ConversionRules.Check(source, target) != ConversionBlock.LossyToLossless
                             && ConversionRules.Check(source, target) != ConversionBlock.SameFormat)
            .SelectMany(source => ConversionRules.AllowedBitrates(source!, target))
            .Distinct()
            .OrderDescending()
            .ToList();

        var previous = SelectedBitrate?.Kbps;
        BitrateOptions.Clear();
        foreach (var kbps in offered)
        {
            BitrateOptions.Add(new BitrateOption(kbps));
        }

        SelectedBitrate = BitrateOptions.FirstOrDefault(option => option.Kbps == previous) ?? BitrateOptions.FirstOrDefault();
        OnPropertyChanged(nameof(HasBitrateChoice));
    }

    private string? ReasonFor(int index)
    {
        if (_sources[index] is not { } source)
            return Strings.ConvertBlockUnreadable;

        var target = SelectedTarget.Target;
        if (ConversionRules.Check(source, target) is { } block)
        {
            return block switch
            {
                ConversionBlock.LossyToLossless => Strings.ConvertBlockLossyToLossless,
                ConversionBlock.SameFormat => Strings.ConvertBlockSameFormat,
                _ => Strings.ConvertBlockNoLowerBitrate,
            };
        }

        if (ConversionRules.IsLossy(target) && SelectedBitrate is { } bitrate
            && !ConversionRules.AllowedBitrates(source, target).Contains(bitrate.Kbps))
            return Strings.ConvertBlockBitrateTooHigh;

        return null;
    }

    private List<int> EligibleIndexes() =>
        Enumerable.Range(0, _tracks.Count).Where(index => ReasonFor(index) is null).ToList();

    private void Refresh()
    {
        if (!IsAvailable)
        {
            EligibleCount = 0;
            StatusMessage = Strings.ConverterUnavailable;
            return;
        }

        if (IsProbing)
        {
            EligibleCount = 0;
            StatusMessage = Strings.ConvertReadingFiles;
            return;
        }

        var eligible = EligibleIndexes();
        EligibleCount = eligible.Count;
        StatusMessage = eligible.Count == 0
            ? (_tracks.Count > 0 ? ReasonFor(0) : null)
            : eligible.Count < _tracks.Count ? Strings.ConvertSkipped(_tracks.Count - eligible.Count, _tracks.Count) : null;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        var indexes = EligibleIndexes();
        var target = SelectedTarget.Target;
        int? bitrate = ConversionRules.IsLossy(target) ? SelectedBitrate?.Kbps : null;
        var keepOriginal = KeepOriginal;
        var batch = indexes.Select(index => _tracks[index]).ToList();

        _cancellation = new CancellationTokenSource();
        IsConverting = true;
        Progress = 0;
        ResultMessage = null;

        var outcomes = new List<ConversionOutcome>();
        var cancelled = false;
        try
        {
            for (var i = 0; i < batch.Count; i++)
            {
                var done = i;
                var progress = new Progress<double>(fraction => Progress = (done + fraction) / batch.Count * 100);
                outcomes.Add(await _service.ConvertAsync(batch[i], target, bitrate, keepOriginal, progress, _cancellation.Token));
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsConverting = false;
        }

        foreach (var failure in outcomes.Where(o => o.Converted is null || o.Error is not null))
        {
            _logger.LogError("Failed to convert {FilePath}: {Error}", failure.Source.FilePath, failure.Error);
        }

        ForgetRemovedOriginals(outcomes);
        ResultMessage = Summarize(outcomes, batch.Count, cancelled);
        Progress = cancelled ? 0 : 100;

        var converted = outcomes.Where(o => o.Converted is not null).ToList();
        if (converted.Count > 0)
        {
            Converted?.Invoke(this, converted);
        }

        Refresh();
    }

    [RelayCommand]
    public void CancelConversion() => _cancellation?.Cancel();

    // An original that went to the trash can't be converted again.
    private void ForgetRemovedOriginals(IEnumerable<ConversionOutcome> outcomes)
    {
        foreach (var removed in outcomes.Where(o => o.OriginalRemoved))
        {
            var index = _tracks.FindIndex(track => track.FilePath == removed.Source.FilePath);
            if (index >= 0)
            {
                _tracks.RemoveAt(index);
                _sources.RemoveAt(index);
            }
        }

        RebuildBitrates();
        UpdateFrom();
    }

    private static string Summarize(IReadOnlyList<ConversionOutcome> outcomes, int attempted, bool cancelled)
    {
        var failures = outcomes.Where(o => o.Converted is null).ToList();
        var converted = outcomes.Count - failures.Count;

        if (failures.Count > 0)
            return Strings.ConvertedSome(converted, attempted, string.Join(", ", failures.Select(f => Path.GetFileName(f.Source.FilePath))));

        if (outcomes.FirstOrDefault(o => o.Error is not null) is { } trashFailure)
            return Strings.ConvertTrashFailed(trashFailure.Error!);

        return cancelled ? Strings.ConvertCancelled : Strings.ConvertedAll(converted);
    }
}
