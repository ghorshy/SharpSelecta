using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpSelecta.App.ViewModels;

public partial class EqualizerBandViewModel : ObservableObject
{
    public EqualizerBandViewModel(int index, string label, double gainDb)
    {
        Index = index;
        Label = label;
        GainDb = gainDb;
    }

    public int Index { get; }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainDbLabel))]
    public partial double GainDb { get; set; }

    public string GainDbLabel => $"{GainDb.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} dB";
}
