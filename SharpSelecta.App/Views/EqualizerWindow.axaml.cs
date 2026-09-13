using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class EqualizerWindow : Window
{
    public EqualizerWindow()
    {
        InitializeComponent();
        BandsPanel.AddHandler(InputElement.PointerReleasedEvent, OnBandSliderPointerReleased, handledEventsToo: true);
    }

    private void OnBandSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is EqualizerViewModel viewModel)
        {
            viewModel.PersistBandGains();
        }
    }
}
