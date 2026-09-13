using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpSelecta.App.ViewModels;

public partial class EqualizerBandViewModel : ObservableObject
{
    public EqualizerBandViewModel(int index, string label, double gainDb)
    {
        Index = index;
        Label = label;
        this.gainDb = gainDb;
    }

    public int Index { get; }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainDbLabel))]
    private double gainDb;

    public string GainDbLabel => $"{GainDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} dB";
}
